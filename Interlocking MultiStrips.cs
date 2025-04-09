using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace InterlockingStrips
{
    public class Interlocking_MultiStrips : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Interlocking_MultiStrips class.
        /// </summary>
        public Interlocking_MultiStrips()
          : base("Interlocking MultiStrips", "M&T3D V2",
              "Creates Mortise & Tenon joint for multiple planar strips",
              "StripLab", "Joineries")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Horizontal Brep", "HorizBrep", "Base horizontal Brep", GH_ParamAccess.item);
            pManager.AddBrepParameter("Vertical Breps", "VertBreps", "List of vertical Breps", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "Thickness", "Thickness of joint boxes", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance", "Tolerance", "Tolerance for scaling", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Edge Index", "EdgeIndex", "Index of the edge to generate joints", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Mirror Plane", "MirrorPlane", "Plane to mirror the box", GH_ParamAccess.item);
            pManager.AddPointParameter("Centroid", "Centroid", "Reference centroid", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Mortise Strip", "Mortise", "Resulting mortise brep", GH_ParamAccess.item);
            pManager.AddBrepParameter("Tenon Strips", "Tenons", "Resulting tenon breps", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep horizBrep = null;
            List<Brep> vertBreps = new List<Brep>();
            double thickness = 0;
            double tolerance = 0;
            int edgeIndex = 0;
            Plane mirrorPlane = Plane.Unset;
            Point3d centroid = Point3d.Unset;

            if (!DA.GetData(0, ref horizBrep)) return;
            if (!DA.GetDataList(1, vertBreps)) return;
            if (!DA.GetData(2, ref thickness)) return;
            if (!DA.GetData(3, ref tolerance)) return;
            if (!DA.GetData(4, ref edgeIndex)) return;
            if (!DA.GetData(5, ref mirrorPlane)) return;
            if (!DA.GetData(6, ref centroid)) return;

            // Output containers
            Brep mortiseStrip = null;
            List<Brep> tenonStrips = new List<Brep>();

            // STEP 1: Create mirrored center boxes
            List<Brep> allBoxes = new List<Brep>();
            int[] indices = { edgeIndex, edgeIndex + 1 };

            foreach (Brep vert in vertBreps)
            {
                if (vert == null) continue;
                foreach (int idx in indices)
                {
                    var boxes = CreateEdgeBoxes(vert, idx, thickness, mirrorPlane);
                    if (boxes == null) continue;

                    foreach (Box box in boxes)
                        allBoxes.Add(box.ToBrep());
                }
            }

            // STEP 2: Subtract boxes from vertical Breps to form tenons
            foreach (Brep vert in vertBreps)
            {
                var result = Brep.CreateBooleanDifference(new List<Brep> { vert }, allBoxes,
                    Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

                if (result != null && result.Length > 0)
                    tenonStrips.AddRange(result);
            }

            // STEP 3: Create scaled intersections
            List<Brep> intersections = new List<Brep>();
            foreach (Brep tenon in tenonStrips)
            {
                var scaled = ScaleIntersection(horizBrep, tenon, 1.0 + tolerance);
                if (scaled != null) intersections.Add(scaled);
            }

            // STEP 4: Subtract from horizontal brep
            var mortiseResult = Brep.CreateBooleanDifference(new List<Brep> { horizBrep }, intersections,
                Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            if (mortiseResult != null && mortiseResult.Length > 0)
                mortiseStrip = mortiseResult[0];

            DA.SetData(0, mortiseStrip);
            DA.SetDataList(1, tenonStrips);
        }

        // ---------------- Helper: CreateEdgeBoxes ------------------
        private List<Box> CreateEdgeBoxes(Brep brep, int edgeIndex, double thickness, Plane mirrorPlane)
        {
            if (brep == null || edgeIndex < 0 || edgeIndex >= brep.Edges.Count) return null;

            Curve edgeCurve = brep.Edges[edgeIndex].DuplicateCurve();
            if (edgeCurve == null || !edgeCurve.IsLinear()) return null;

            edgeCurve.LengthParameter(edgeCurve.GetLength() / 2.0, out double midT);
            Point3d midPt = edgeCurve.PointAt(midT);

            Vector3d edgeDir = edgeCurve.TangentAt(midT); edgeDir.Unitize();

            Vector3d up = Vector3d.ZAxis;
            Vector3d right = Vector3d.CrossProduct(edgeDir, up);
            if (right.IsTiny())
            {
                up = Vector3d.YAxis;
                right = Vector3d.CrossProduct(edgeDir, up);
            }
            right.Unitize(); up = Vector3d.CrossProduct(right, edgeDir); up.Unitize();

            Plane boxPlane = new Plane(midPt, right, up);
            Box box1 = new Box(boxPlane,
                new Interval(-thickness / 2, thickness / 2),
                new Interval(-thickness / 2, thickness / 2),
                new Interval(-thickness / 2, thickness / 2));

            Box box2 = box1;
            box2.Transform(Transform.Mirror(mirrorPlane));

            return new List<Box> { box1, box2 };
        }

        // ---------------- Helper: ScaleIntersection ------------------
        private Brep ScaleIntersection(Brep A, Brep B, double scale)
        {
            var intersection = Brep.CreateBooleanIntersection(A, B,
                Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            if (intersection == null || intersection.Length == 0) return null;

            Brep interBrep = intersection[0];
            var vmp = VolumeMassProperties.Compute(interBrep);
            if (vmp == null) return null;

            Transform scaleTransform = Transform.Scale(vmp.Centroid, scale);
            Brep scaled = interBrep.DuplicateBrep();
            scaled.Transform(scaleTransform);

            return scaled;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                using (MemoryStream ms = new MemoryStream(Properties.Resources.ICN_05))
                {
                    Bitmap bmp = new Bitmap(ms);
                    return new Bitmap(bmp, new Size(24, 24));
                }
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("C425271F-A71A-4996-9C1D-2B9FE6F82F7B"); }
        }
    }
}
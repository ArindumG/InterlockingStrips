using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace InterlockingStrips
{
  public class InterlockingStripsComponent : GH_Component
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public InterlockingStripsComponent()
      : base("Dowelled M&T", "M&T3D",
        "Creates Mortise & Tenon joint for two planar strips",
        "StripLab", "Joineries")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
            pManager.AddBrepParameter("HorizBrep", "H", "Horizontal Brep", GH_ParamAccess.item);
            pManager.AddBrepParameter("VertBrep", "V", "Vertical Brep", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness", "T", "Joint Thickness", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance", "Tol", "Joint Tolerance", GH_ParamAccess.item);
        }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
            pManager.AddBrepParameter("MortiseStrip", "M", "Mortise Strip Output", GH_ParamAccess.item);
            pManager.AddBrepParameter("TenonStrip", "T", "Tenon Strip Output", GH_ParamAccess.item);
        
    }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            {
                Brep horizBrep = null, vertBrep = null;
                double thickness = 0, tolerance = 0;

                if (!DA.GetData(0, ref horizBrep)) return;
                if (!DA.GetData(1, ref vertBrep)) return;
                if (!DA.GetData(2, ref thickness)) return;
                if (!DA.GetData(3, ref tolerance)) return;

                // 1. Compute centroid & mirror plane
                var vmp = VolumeMassProperties.Compute(vertBrep);
                if (vmp == null) return;

                var centroid = vmp.Centroid;
                var mirrorPlane = new Plane(centroid, Vector3d.XAxis, Vector3d.ZAxis);

                // 2. Create edge boxes
                int edgeIndex = 6;
                var boxes = CreateEdgeBoxes(vertBrep, edgeIndex, thickness, mirrorPlane, centroid);
                if (boxes == null || boxes.Count == 0) return;

                var boxBreps = new List<Brep>();
                foreach (var box in boxes)
                    boxBreps.Add(box.ToBrep());

                // 3. Create Tenon
                var tenon = Brep.CreateBooleanDifference(new List<Brep> { vertBrep }, boxBreps, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                if (tenon == null || tenon.Length == 0) return;
                Brep localTenon = tenon[0];

                // 4. Scaled intersection
                Brep scaledIntersection = ScaleIntersection(horizBrep, localTenon, tolerance + 1);
                if (scaledIntersection == null) return;

                // 5. Create Mortise
                var mortise = Brep.CreateBooleanDifference(new List<Brep> { horizBrep }, new List<Brep> { scaledIntersection }, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                if (mortise == null || mortise.Length == 0) return;

                DA.SetData(0, mortise[0]);
                DA.SetData(1, localTenon);
            }
        }
            

    private List<Box> CreateEdgeBoxes(Brep brep, int edgeIndex, double thickness, Plane mirrorPlane, Point3d centroid)
        {
            if (brep == null || brep.Edges.Count == 0 || edgeIndex < 0 || edgeIndex >= brep.Edges.Count) return null;

            Curve edge = brep.Edges[edgeIndex].DuplicateCurve();
            if (edge == null || !edge.IsLinear()) return null;

            edge.LengthParameter(edge.GetLength() / 2, out double t);
            Point3d midPt = edge.PointAt(t);

            Vector3d edgeDir = edge.TangentAt(t);
            edgeDir.Unitize();

            Vector3d up = Vector3d.ZAxis;
            Vector3d right = Vector3d.CrossProduct(edgeDir, up);
            if (right.IsTiny())
            {
                up = Vector3d.YAxis;
                right = Vector3d.CrossProduct(edgeDir, up);
            }

            right.Unitize();
            up = Vector3d.CrossProduct(right, edgeDir);
            up.Unitize();

            Plane boxPlane = new Plane(midPt, right, up);
            Box box1 = new Box(boxPlane, new Interval(-thickness, thickness), new Interval(-thickness, thickness), new Interval(-thickness / 2, thickness / 2));

            Transform mirror = Transform.Mirror(mirrorPlane);
            Box box2 = box1;
            box2.Transform(mirror);

            return new List<Box> { box1, box2 };
        }

        private Brep ScaleIntersection(Brep A, Brep B, double scaledTol)
        {
            if (A == null || B == null || scaledTol < 1 || scaledTol > 2) return null;

            var intersection = Brep.CreateBooleanIntersection(new List<Brep> { A }, new List<Brep> { B }, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            if (intersection == null || intersection.Length == 0) return null;

            Brep inter = intersection[0];
            var vmp = VolumeMassProperties.Compute(inter);
            if (vmp == null) return null;

            var scale = Transform.Scale(vmp.Centroid, scaledTol);
            inter.Transform(scale);
            return inter;
        
    }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>

        protected override Bitmap Icon
        {
            get
            {
                using (MemoryStream ms = new MemoryStream(Properties.Resources.ICN_01))
                {
                    Bitmap bmp = new Bitmap(ms);
                    return new Bitmap(bmp, new Size(24, 24));
                }
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("4216acf5-6f29-4754-b674-5d14070131d2");
  }
}
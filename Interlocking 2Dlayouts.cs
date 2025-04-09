using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace InterlockingStrips
{
    public class Interlocking_2Dlayouts : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Interlocking_2Dlayouts class.
        /// </summary>
        public Interlocking_2Dlayouts()
          : base("M&T layouts", "M&T2D",
              "Creates 2D layouts of planar strips with Mortise & Tenon joints",
              "StripLab", "Fabrication")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("VertBrep", "V", "Vertical Brep", GH_ParamAccess.item);
            pManager.AddBrepParameter("HorizBrep", "H", "Horizontal Brep", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("ProjectedVertCurves", "A", "Projected curves from rotated vertical Brep", GH_ParamAccess.list);
            pManager.AddCurveParameter("ProjectedHorizCurves", "B", "Projected curves from horizontal Brep", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep vertBrep = null;
            Brep horizBrep = null;

            if (!DA.GetData(0, ref vertBrep)) return;
            if (!DA.GetData(1, ref horizBrep)) return;

            if (vertBrep == null || horizBrep == null) return;

            // Compute centroid of vertical Brep
            var vmp = VolumeMassProperties.Compute(vertBrep);
            if (vmp == null) return;
            Point3d centroid = vmp.Centroid;

            // Rotate 90 degrees around Y-axis
            Transform rotation = Transform.Rotation(RhinoMath.ToRadians(90), Vector3d.YAxis, centroid);
            Brep rotatedBrep = vertBrep.DuplicateBrep();
            rotatedBrep.Transform(rotation);

            // Projection plane
            Plane xyPlane = Plane.WorldXY;

            // Project rotated VertBrep onto XY plane
            List<Curve> vertCurves = new List<Curve>();
            foreach (BrepEdge edge in rotatedBrep.Edges)
            {
                Curve projected = edge.ToNurbsCurve().DuplicateCurve();
                projected.Transform(Transform.PlanarProjection(xyPlane));
                vertCurves.Add(projected);
            }
            Curve[] joinedVertCurves = Curve.JoinCurves(vertCurves);

            // Project HorizBrep onto XY plane
            List<Curve> horizCurves = new List<Curve>();
            foreach (BrepEdge edge in horizBrep.Edges)
            {
                Curve projected = edge.ToNurbsCurve().DuplicateCurve();
                projected.Transform(Transform.PlanarProjection(xyPlane));
                horizCurves.Add(projected);
            }
            Curve[] joinedHorizCurves = Curve.JoinCurves(horizCurves);

            DA.SetDataList(0, joinedVertCurves);
            DA.SetDataList(1, joinedHorizCurves);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                using (MemoryStream ms = new MemoryStream(Properties.Resources.ICN_02))
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
            get { return new Guid("ED38C678-9A9E-4108-883A-67EC745020A4"); }
        }
    }
}
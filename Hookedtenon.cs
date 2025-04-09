using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using System.IO;

namespace InterlockingStrips
{
    public class Hookedtenon : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Hookedtenon class.
        /// </summary>
        public Hookedtenon()
          : base("CustomTenon", "CTenon",
              "Creates 2D Custom hook joint at the end of a strip",
              "StripLab", "Fabrication")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("BaseGeometry", "G", "Optional input geometry to override base rectangle", GH_ParamAccess.item);
            pManager[0].Optional = true;

            pManager.AddNumberParameter("BaseHeight", "BH", "Height of base rectangle", GH_ParamAccess.item, 10.0);
            pManager.AddNumberParameter("StepOffset", "SO", "Offset for the step", GH_ParamAccess.item, 4.0);
            pManager.AddNumberParameter("StepWidth", "SW", "Width of the step", GH_ParamAccess.item, 4.0);
            pManager.AddNumberParameter("StepHeight", "SH", "Height of the step", GH_ParamAccess.item, 10.0);

            pManager.AddNumberParameter("BaseX", "BX", "X position of slanted section", GH_ParamAccess.item, 16.0);
            pManager.AddNumberParameter("BaseY", "BY", "Y position of slanted section", GH_ParamAccess.item, 18.0);
            pManager.AddNumberParameter("SlopeHeight", "SLH", "Height of the slope", GH_ParamAccess.item, 10.0);

            pManager.AddBooleanParameter("Mirror", "M", "Mirror the section geometry", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Mode", "Mode", "Choose between Option A or Option B", GH_ParamAccess.item, "Option A");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("SectionCurves", "C", "Generated section curves", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GeometryBase baseGeom = null;
            double baseHeight = 0, stepOffset = 0, stepWidth = 0, stepHeight = 0;
            double baseX = 0, baseY = 0, slopeHeight = 0;
            bool mirror = false;
            string mode = "Option A";

            // Access inputs
            DA.GetData(0, ref baseGeom);
            DA.GetData(1, ref baseHeight);
            DA.GetData(2, ref stepOffset);
            DA.GetData(3, ref stepWidth);
            DA.GetData(4, ref stepHeight);
            DA.GetData(5, ref baseX);
            DA.GetData(6, ref baseY);
            DA.GetData(7, ref slopeHeight);
            DA.GetData(8, ref mirror);
            DA.GetData(9, ref mode);

            List<Curve> stripSections = new List<Curve>();

            // Determine base rectangle from input or fallback
            Curve baseCurve = null;
            double baseWidth = 0;

            if (baseGeom != null && baseGeom is Curve inputCurve && inputCurve.IsClosed)
            {
                baseCurve = inputCurve;
                BoundingBox bbox = baseCurve.GetBoundingBox(true);
                baseWidth = bbox.Max.X - bbox.Min.X;
                baseHeight = bbox.Max.Y - bbox.Min.Y;
                stripSections.Add(baseCurve.DuplicateCurve());
            }
            else
            {
                // Default base rectangle if no geometry provided
                Point3d p0 = new Point3d(0, 0, 0);
                Point3d p2 = new Point3d(28, baseHeight, 0);
                baseWidth = 28;
                Rectangle3d baseSection = new Rectangle3d(Plane.WorldXY, p0, p2);
                stripSections.Add(baseSection.ToNurbsCurve());
            }

            // === Step Section ===
            Point3d s0 = new Point3d(stepOffset, baseHeight, 0);
            Point3d s1 = new Point3d(stepOffset + stepWidth, baseHeight, 0);
            Point3d s2 = new Point3d(stepOffset + stepWidth, baseHeight + stepHeight, 0);
            Point3d s3 = new Point3d(stepOffset, baseHeight + stepHeight, 0);
            Polyline stepShape = new Polyline(new List<Point3d> { s0, s1, s2, s3, s0 });
            stripSections.Add(stepShape.ToNurbsCurve());

            if (mode == "Option B")
            {
                double slopeWidth = baseWidth - baseX;
                Point3d basePoint = new Point3d(baseX, baseY, 0);
                Point3d sp1 = new Point3d(baseX + slopeWidth, baseY, 0);
                Point3d sp2 = new Point3d(baseX, baseY + slopeHeight, 0);

                Polyline slantedShape = new Polyline(new List<Point3d> { basePoint, sp1, sp2, basePoint });
                stripSections.Add(slantedShape.ToNurbsCurve());
            }

            // === Mirror (Optional) ===
            if (mirror)
            {
                Transform mirrorTransform = Transform.Mirror(new Plane(Point3d.Origin, Vector3d.XAxis));
                List<Curve> mirroredSections = new List<Curve>();
                foreach (Curve c in stripSections)
                {
                    Curve mirrored = c.DuplicateCurve();
                    mirrored.Transform(mirrorTransform);
                    mirroredSections.Add(mirrored);
                }
                stripSections.AddRange(mirroredSections);
            }

            DA.SetDataList(0, stripSections);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                using (MemoryStream ms = new MemoryStream(Properties.Resources.I ))
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
            get { return new Guid("836D556A-60DB-4D24-8CE8-211B9C8C287A"); }
        }
    }
}
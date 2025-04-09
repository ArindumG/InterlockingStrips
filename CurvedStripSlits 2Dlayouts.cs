using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace InterlockingStrips
{
    public class CurvedStripSlits2Dlayouts : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public CurvedStripSlits2Dlayouts()
          : base("CurvedStrip Slits layout ", "CrvSlt2D",
              "Creates 2D layout for curved strips with slits",
              "StripLab", "Fabrication")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("CurvedBrep", "B", "Input curved Brep", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Index", "I", "Face index to unroll", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Strip2D", "S", "Unrolled 2D curve of the face", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Declare input variables
            Brep curvedBrep = null;
            int index = -1;

            // 2. Access input
            if (!DA.GetData(0, ref curvedBrep)) return;
            if (!DA.GetData(1, ref index)) return;

            if (curvedBrep == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input Brep is null.");
                return;
            }

            if (index < 0 || index >= curvedBrep.Faces.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Index out of range.");
                return;
            }

            // 3. Duplicate face
            BrepFace targetFace = curvedBrep.Faces[index];
            Brep faceBrep = targetFace.DuplicateFace(false);

            // 4. Unroll face
            Unroller unroll = new Unroller(faceBrep);

            Curve[] unrolledCurves;
            Point3d[] unrolledPoints;
            TextDot[] unrolledDots;
            Brep[] unrolledBreps = unroll.PerformUnroll(out unrolledCurves, out unrolledPoints, out unrolledDots);

            // 5. Join boundary and output
            if (unrolledBreps != null && unrolledBreps.Length > 0)
            {
                Curve[] edges = unrolledBreps[0].DuplicateEdgeCurves(true);
                Curve[] joined = Curve.JoinCurves(edges);
                if (joined.Length > 0)
                {
                    DA.SetData(0, joined[0]);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Edge joining failed.");
                }
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Unroll failed.");
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                using (MemoryStream ms = new MemoryStream(Properties.Resources.ICN_04))
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
            get { return new Guid("4407347A-8C89-4251-9B23-2A8A64F71460"); }
        }
    }
}
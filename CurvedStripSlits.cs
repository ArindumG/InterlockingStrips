using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace InterlockingStrips
{
    public class CurvedStripSlits : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public CurvedStripSlits()
          : base("Curved Strip Slits", "CrvStrpSlits",
              "Add slits to curved strips",
              "StripLab", "Joineries")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep A", "A", "First Brep", GH_ParamAccess.item);
            pManager.AddBrepParameter("Brep B", "B", "Second Brep", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance", "T", "Scaling Tolerance", GH_ParamAccess.item, 0.05);
            pManager.AddIntegerParameter("Edge Index", "E", "Index of edge to scale/move", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Curved Brep A", "CA", "Modified Brep A", GH_ParamAccess.item);
            pManager.AddBrepParameter("Curved Brep B", "CB", "Modified Brep B", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep brepA = null;
            Brep brepB = null;
            double tolerance = 0.05;
            int edgeIndex = 0;

            if (!DA.GetData(0, ref brepA)) return;
            if (!DA.GetData(1, ref brepB)) return;
            if (!DA.GetData(2, ref tolerance)) return;
            if (!DA.GetData(3, ref edgeIndex)) return;

            Brep[] intersection = Brep.CreateBooleanIntersection(brepA, brepB, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            if (intersection == null || intersection.Length == 0)
                return;

            Brep scaledInterBrep = intersection[0].DuplicateBrep();
            BoundingBox bbox = scaledInterBrep.GetBoundingBox(true);
            Point3d center = bbox.Center;
            Transform scale = Transform.Scale(center, 1.0 + tolerance);
            scaledInterBrep.Transform(scale);

            if (edgeIndex < 0 || edgeIndex >= scaledInterBrep.Edges.Count)
                return;

            Curve selectedEdge = scaledInterBrep.Edges[edgeIndex].DuplicateCurve();
            double edgeLength = selectedEdge.GetLength();
            Vector3d direction = selectedEdge.PointAtEnd - selectedEdge.PointAtStart;
            direction.Unitize();

            Point3d midPt = selectedEdge.PointAtNormalizedLength(0.5);
            Vector3d edgeDir = direction;

            Vector3d up = Vector3d.ZAxis;
            if (Math.Abs(Vector3d.Multiply(edgeDir, up)) > 0.99)
                up = Vector3d.XAxis;

            Vector3d xAxis = Vector3d.CrossProduct(up, edgeDir); xAxis.Unitize();
            Vector3d yAxis = Vector3d.CrossProduct(edgeDir, xAxis); yAxis.Unitize();

            Plane scalePlane = new Plane(midPt, xAxis, yAxis);
            Transform scale1D = Transform.Scale(scalePlane, 1.0, 1.0, 0.5);

            Brep scaled1DBrep = scaledInterBrep.DuplicateBrep();
            scaled1DBrep.Transform(scale1D);

            double moveFactor = 0.25;
            double moveAmount = moveFactor * edgeLength;
            Vector3d moveVectorPos = direction * moveAmount;
            Vector3d moveVectorNeg = -direction * moveAmount;

            Brep movedBrepPos = scaled1DBrep.DuplicateBrep();
            movedBrepPos.Transform(Transform.Translation(moveVectorPos));

            Brep movedBrepNeg = scaled1DBrep.DuplicateBrep();
            movedBrepNeg.Transform(Transform.Translation(moveVectorNeg));

            Brep[] resultA = Brep.CreateBooleanDifference(brepA, movedBrepPos, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            Brep[] resultB = Brep.CreateBooleanDifference(brepB, movedBrepNeg, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            DA.SetData(0, (resultA != null && resultA.Length > 0) ? resultA[0] : null);
            DA.SetData(1, (resultB != null && resultB.Length > 0) ? resultB[0] : null);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                using (MemoryStream ms = new MemoryStream(Properties.Resources.ICN_03))
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
            get { return new Guid("30991CCA-8244-49DF-8492-E6765319679B"); }
        }
    }
}
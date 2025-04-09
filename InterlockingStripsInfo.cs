using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace InterlockingStrips
{
  public class InterlockingStripsInfo : GH_AssemblyInfo
  {
    public override string Name => "InterlockingStrips";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon => null;

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "";

    public override Guid Id => new Guid("bd0ed386-7105-48fe-87cc-6a3e28181e97");

    //Return a string identifying you or your company.
    public override string AuthorName => "";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "";

    //Return a string representing the version.  This returns the same version as the assembly.
    public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
  }
}
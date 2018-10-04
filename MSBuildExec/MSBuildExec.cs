/////////////////////////////////////////////////////////////////////
// MSBuildExec.cs : builds projects using .csproj or .xml config   //
// v1.0                                                            //
// Kaiqi Zhang, CSE681 - Software Modeling and Analysis, Fall 2017 //
// Ammar Salman                                                    //
/////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.Execution;

namespace RemoteBuildServer
{
  ///////////////////////////////////////////////////////////////////
  // MSBuildExec class for MSBuild Engine
  //
  public class MSBuildExec
  {
    /* 
     * This method uses MSBuild to build a .csproj file.
     * The csproj file is configured to build as Debug/AnyCPU
     * Therefore, there is no need to specify the parameters here.
     * This is useful for the build server because it should be as
     * general as it can get. The build server shouldn't have to
     * specify different build parameters for each project. 
     * Instead, the csproj/xml file sets the configuration settings.
     * 
     * In the csproj file, the OutputPath is set to "csproj_Debug" 
     * for the Debug configuration, and "csproj_Release" for the
     * Release configuration. Moreover, if Debug was selected, the
     * project will be build into an x86 library (DLL), while if Release
     * was selected, the project will build into an x64 executable (EXE)
     * 
     * To change the default configuration, the first PropertyGroup
     * in the ..\..\..\files\Builder.csproj must be modified.
     */
    static public string BuildCsproj(string projectFileName, string logFileName)
    {
      ConsoleLogger logger = new ConsoleLogger();
      FileLogger fLogger = new FileLogger() { Parameters = "logfile=" + logFileName };

      Dictionary<string, string> GlobalProperty = new Dictionary<string, string>();
      BuildRequestData BuildRequest = new BuildRequestData(projectFileName, GlobalProperty, null, new string[] { "Rebuild" }, null);
      BuildParameters bp = new BuildParameters();
      bp.Loggers = new List<ILogger> { logger, fLogger }.AsEnumerable();

      BuildResult buildResult = BuildManager.DefaultBuildManager.Build(bp, BuildRequest);

      //Console.WriteLine();
      return buildResult.OverallResult.ToString();
    }

    /* 
     * This method uses MSBuild to build a .xml file.
     * The xml file is configured to build as Release/x64
     * 
     * In the xml file, the OutputPath is set to "xml_Debug" 
     * for the Debug configuration, and "xml_Release" for the
     * Release configuration. Moreover, if Debug was selected, the
     * project will be build into an x86 library (DLL), while if Release
     * was selected, the project will build into an x64 executable (EXE)
     * 
     * To change the default configuration, the first PropertyGroup
     * in the ..\..\..\files\project.xml must be modified.
     */
    static public string BuildXml(string projectFileName, string logFileName)
    {
      ConsoleLogger logger = new ConsoleLogger();
      FileLogger fLogger = new FileLogger() { Parameters = "logfile=" + logFileName };

      Dictionary<string, string> GlobalProperty = new Dictionary<string, string>();
      BuildRequestData BuildRequest = new BuildRequestData(projectFileName, GlobalProperty, null, new string[] { "Rebuild" }, null);
      BuildParameters bp = new BuildParameters();
      bp.Loggers = new List<ILogger> { logger, fLogger }.AsEnumerable();

      BuildResult buildResult = BuildManager.DefaultBuildManager.Build(bp, BuildRequest);

      //Console.WriteLine();
      return buildResult.OverallResult.ToString();
    }
    //----< test stub >------------------------------------------------

#if (TEST_BUILDENGINE)
    static void Main(string[] args)
    {
      Console.ForegroundColor = ConsoleColor.DarkGreen;
      Console.BackgroundColor = ConsoleColor.White;
      Console.Write("\n  Building Builder.csproj ");
      Console.Write("\n =========================\n\n");
      Console.ResetColor();
      try
      {
        string projectFileName = @"..\..\files\Builder.csproj";
        BuildCsproj(projectFileName, @"..\..\files\Builder.log");
      }
      catch (Exception ex)
      {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.BackgroundColor = ConsoleColor.White;
        Console.Write("\n\n  An error occured while trying to build the csproj file.\n  Details: {0}\n\n", ex.Message);
        Console.ResetColor();
      }

      Console.ForegroundColor = ConsoleColor.DarkGreen;
      Console.BackgroundColor = ConsoleColor.White;
      Console.Write("\n  Building project.xml ");
      Console.Write("\n ======================\n\n");
      Console.ResetColor();
      try
      {
        string projectFileName = @"..\..\files\project.xml";
        BuildXml(projectFileName, @"..\..\files\project.log");
      }
      catch (Exception ex)
      {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.BackgroundColor = ConsoleColor.White;
        Console.Write("\n\n  An error occured while trying to build the xml file.\n  Details: {0}\n\n", ex.Message);
        Console.ResetColor();
      }

      Console.ReadLine();
    }
#endif
  }
}

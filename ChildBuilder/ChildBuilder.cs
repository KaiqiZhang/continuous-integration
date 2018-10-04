///////////////////////////////////////////////////////////////////////////
// ChildBuilder.cs - Child builder for code federation                   //
// Ver 1.0                                                               //
// Kaiqi Zhang, Jim Fawcett                                              //
// CSE681 Software Modeling & Analysis, Fall 2017                        //
///////////////////////////////////////////////////////////////////////////
/*
 * Purpose:
 * --------
 * Child builder for code federation. This application is started by a mother
 * builder program in a process pool. It can parse test request, retrieve files
 * from repo server, and build source code into dll files.
 * 
 * Package Operations:
 * -------------------
 * This package uses WCF for communication. Make sure to run as Administrator
 * because the communication service requires it.
 * It opens a port at 8090, 8091, 8092... for each child builder.
 * 
 * Required Files:
 * ---------------
 * - ChildBuilder.cs         - GUI for start builder, send request
 * - MPCommService.cs        - sends and receives messages and files
 * - TestHarnessMessages.cs  - defines the test request message format
 * - Serialization.cs        - serializing and deserializing complex data structures
 * - TestUtilities.cs        - helper functions used mostly for testing
 * 
 * Maintenance History:
 * --------------------
 * Ver 1.1 : 06 Dec 2017
 * - real build process
 * Ver 1.0 : 29 Oct 2017
 * - first release
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

using MessagePassingComm;

namespace RemoteBuildServer
{
  using Msg = CommMessage;

  ///////////////////////////////////////////////////////////////////
  // ChildBuilder class

  class ChildBuilder
  {
    Comm comm_ = null;
    MessageDispatcher dispatcher_ = new MessageDispatcher();
    int id;

    /*----< initializes server message dispatching >---------------*/

    public ChildBuilder(int id)
    {
      Console.Title = "Child Builder " + id;
      this.id = id;

      // set storage directory
      ServiceEnvironment.fileStorage = "../../../Child" + id.ToString() + "Storage/";
      ClientEnvironment.fileStorage = ServiceEnvironment.fileStorage;

      string path = Path.GetFullPath(ServiceEnvironment.fileStorage);
      Console.Write("\n  Child {0} Storage Path: {1}\n", id, path);
      if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

      initializeDispatcher();
    }
    /*----< server finalizer >-------------------------------------*/
    /*
     *  Exception handling is necessary because WCF will throw
     *  an Exception if two servers are started.  That's because
     *  you can only start one listener, per port, per machine.
     */
    ~ChildBuilder()
    {
      try
      {
        comm_.close();
      }
      finally
      {
        /* nothing to do - just preventing unhandle exception */
      }
    }
    /*----< create comm if needed >--------------------------------*/
    /*
     *  You can only start one listerner on a given port for this machine.
     *  If two instances of server are started, that is attempted,
     *  resulting in a WCF exception.  In order to shutdown cleanly
     *  in this circumstance, we need to surppress Finialization.
     */
    void createCommIfNeeded()
    {
      try
      {
        if (comm_ == null)
        {
          string serverMachine = "http://localhost";
          int serverPort = 8090 + id;
          comm_ = new Comm(serverMachine, serverPort);
        }
      }
      catch (Exception ex)
      {
        Console.Write("\n-- {0}", ex.Message);
        GC.SuppressFinalize(this);
        System.Diagnostics.Process.GetCurrentProcess().Close();
      }
    }
    /*----< here server responses to messages are defined >--------*/

    void initializeDispatcher()
    {
      // testRequest
      Func<Msg, Msg> action1 = (Msg msg) =>
      {
        if (msg.type == Msg.MessageType.request)
          processTestRequest(msg);
        Msg returnMsg = new Msg(Msg.MessageType.noReply);
        returnMsg.to = msg.from;
        returnMsg.from = msg.to;
        returnMsg.argument = msg.argument;
        returnMsg.command = msg.command;
        return returnMsg;
      };
      dispatcher_.addCommand(Msg.Command.testRequest, action1);

      // send file
      Func<Msg, Msg> action2 = (Msg msg) =>
      {
        if (msg.type == Msg.MessageType.request)
        {
          foreach (string fileName in msg.arguments)
            comm_.postFile(msg.from, fileName);
          Msg returnMsg = new Msg(Msg.MessageType.noReply);
          returnMsg.to = msg.from;
          returnMsg.from = msg.to;
          returnMsg.argument = msg.argument;
          returnMsg.arguments = msg.arguments;
          returnMsg.command = msg.command;
          return returnMsg;
        }
        else
        {
          filesReceived(msg);
          Msg reply = new Msg(Msg.MessageType.noReply);
          return reply;
        } 
      };
      dispatcher_.addCommand(Msg.Command.sendFile, action2);
    }
    /*----< start comm and receive thread >------------------------*/

    bool start()
    {
      try
      {
        createCommIfNeeded();
        Thread rcvThrd = new Thread(threadProc);
        rcvThrd.IsBackground = true;
        rcvThrd.Start();
        return true;
      }
      catch (Exception ex)
      {
        Console.Write("\n  -- {0}", ex.Message);
        return false;
      }
    }
    /*----< filters messages to which server replies >-------------*/

    bool doReply(Msg msg, Msg reply)
    {
      if (msg.type == Msg.MessageType.noReply)
        return false;
      if (msg.type == Msg.MessageType.connect)
        return false;
      if (reply.type == Msg.MessageType.procError)
        return false;
      return true;
    }
    /*----< receive thread processing >----------------------------*/

    void threadProc()
    {
      while (true)
      {
        try
        {
          CommMessage msg = comm_.getMessage();
          Console.Write("\n  Received {0} message : {1}", msg.type.ToString(), msg.command.ToString());
          CommMessage reply = dispatcher_.doCommand(msg.command, msg);
          if (reply.command == Msg.Command.show)
          {
            reply.show();
            Console.Write("  -- no reply sent");
          }
          if (doReply(msg, reply))
            comm_.postMessage(reply);
        }
        catch
        {
          break;
        }
      }
    }
    /*----< parse request to get filenames of all requested files >-----*/

    private List<string> getFileList(TestRequest request)
    {
      List<string> files = new List<string>();
      foreach (TestElement te in request.tests)
      {
        if (te.buildConfig != null) files.Add(te.buildConfig);
        if (te.testDriver != null) files.Add(te.testDriver);
        foreach (string testCode in te.testCodes)
          files.Add(testCode);
      }
      return files;
    }
    /*----< request files from repo and do build >----------------------*/

    void processTestRequest(Msg msg)
    {
      Console.Write("\n" + msg.argument);

      // parse test request
      TestRequest request = msg.argument.FromXml<TestRequest>();

      if (request != null) // valid test request got
      {
        // get files from repo
        List<string> files = getFileList(request);
        Msg reqMsg = new Msg(Msg.MessageType.request);
        reqMsg.to = "http://localhost:8082/IPluggableComm";
        reqMsg.from = msg.to;
        reqMsg.command = Msg.Command.sendFile;
        reqMsg.argument = msg.argument; // append test request in file request
        foreach (string file in files)
          reqMsg.arguments.Add(file);
        comm_.postMessage(reqMsg);
      }
    }
    /*----< start to build after all files received >--------------------*/

    void filesReceived(Msg msg)
    {
      // create a new thread for mock building
      Thread buildThrd = new Thread(
        () =>
        {
          // parse test request
          TestRequest request = msg.argument.FromXml<TestRequest>();

          if (request != null) // valid test request got
          {
            Console.Write("\n  Parse test request successfully!");
            string buildResult = "Failure"; // Success/Failure

            foreach (TestElement te in request.tests)
            {
              string configName = te.buildConfig;
              string configSpec = Path.Combine(ServiceEnvironment.fileStorage, configName);
              string logName = Path.GetFileNameWithoutExtension(configName)
                               + "-BuildLog-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + ".log";
              string logSpec = Path.Combine(ServiceEnvironment.fileStorage, logName);

              Console.Write("\n  Building with config file: {0}\n", configName);
              buildResult = tryBuild(configSpec, logSpec);
              sendBuildLog(logName);  // send build logs to repo
              notifyClient(msg, configName, buildResult);  // notify client build result

              if (buildResult == "Success")
              {
                TestRequest tr = new TestRequest();
                tr.author = request.author;
                tr.tests.Add(te);
                // send test request to test harness
                sendRequestToTH(tr, msg);
              }
            }
          }
          Console.Write("\n\n  Build finished! Child ready now\n");
          // send ready message
          sendReadyMsg(msg);
        }
      );
      buildThrd.Start();
    }
    /*----< send a ready msg to mother so mother can put it into schedule >----*/

    void sendReadyMsg(Msg msg)
    {
      Msg readyMsg = new Msg(Msg.MessageType.reply);
      readyMsg.to = "http://localhost:8080/IPluggableComm";
      readyMsg.from = msg.to;
      readyMsg.argument = id.ToString();
      readyMsg.command = Msg.Command.ready;

      try
      {
        createCommIfNeeded();
        comm_.postMessage(readyMsg);
      }
      catch
      {
        Console.Write("\n  Error: can't connect");
      }
    }
    /*----< send build log file to repository >--------------------------*/

    void sendBuildLog(string logName)
    {
      Console.Write("\n  Send log file to repo: {0}", logName);

      string repoEndPoint = "http://localhost:8082/IPluggableComm";
      try
      {
        createCommIfNeeded();
        comm_.postFile(repoEndPoint, logName);
      }
      catch
      {
        Console.Write("\n  Error: can't connect");
      }
    }
    /*----< notify the client with build result >----------------------*/

    void notifyClient(Msg msg, string configName, string result)
    {
      Msg readyMsg = new Msg(Msg.MessageType.noReply);
      readyMsg.to = "http://localhost:8081/IPluggableComm";
      readyMsg.from = msg.to;
      readyMsg.argument = Path.GetFileNameWithoutExtension(configName) + " build: " + result;
      readyMsg.command = Msg.Command.buildResult;

      try
      {
        createCommIfNeeded();
        comm_.postMessage(readyMsg);
      }
      catch
      {
        Console.Write("\n  Error: can't connect");
      }
    }
    /*----< send test requests to TestHarness >-----------------------*/

    private void sendRequestToTH(TestRequest tr, Msg msg)
    {
      Msg trMsg = new Msg(Msg.MessageType.request);
      trMsg.to = "http://localhost:8083/IPluggableComm";
      trMsg.from = msg.to;
      trMsg.argument = tr.ToXml();
      trMsg.command = Msg.Command.testRequest;

      try
      {
        createCommIfNeeded();
        comm_.postMessage(trMsg);
      }
      catch
      {
        Console.Write("\n  Error: can't connect");
      }
    }
    //----< call the build engine and get results >-------------------

    private string tryBuild(string configSpec, string logSpec)
    {
      string buildResult = "";
      try
      {
        buildResult = MSBuildExec.BuildCsproj(configSpec, logSpec);
        
        if (buildResult == "Success")
          Console.Write("\n  Build successfully!\n");
        else
          Console.Write("\n  Build failed!\n");
      }
      catch (Exception ex)
      {
        Console.Write("\n  An error occured while trying to build the xml file.\n  Details: {0}\n\n", ex.Message);
        Console.Write("\n  Build failed!\n");
      }

      return buildResult;
    }

    /*----< starts the server process >----------------------------*/

    static void Main(string[] args)
    {
      Console.Write("\n  Child Builder Process");
      Console.Write("\n =======================");

      if (args.Count() == 0)
      {
        Console.Write("\n  please enter integer value on command line");
        return;
      }
      else
      {
        Console.Write("\n  Starting child builder {0}", args[0]);
        ChildBuilder server = new ChildBuilder(int.Parse(args[0]));
        if (!server.start())
          return;
      }

      Console.Write("\n  Press a key to exit");
      Console.ReadKey();
      TestUtilities.putLine();
    }
  }
}

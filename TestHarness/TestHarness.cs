///////////////////////////////////////////////////////////////////////////
// TestHarness.cs - Mock Tester for code federation                      //
// Ver 1.0                                                               //
// Kaiqi Zhang, Jim Fawcett                                              //
// CSE681 Software Modeling & Analysis, Fall 2017                        //
///////////////////////////////////////////////////////////////////////////
/*
 * Purpose:
 * --------
 * TestHarness server to test dll file and generate logs.
 * attempt to load and execute each test library, catching any 
 * execeptions that may be emitted, and report sucess or failure and 
 * any exception messages, to the Console.
 * 
 * Package Operations:
 * -------------------
 * This package uses WCF for communication. Make sure to run as Administrator
 * because the communication service requires it.
 * It opens a port at 8082.
 * 
 * Required Files:
 * ---------------
 * - TestHarness.cs          - test harness server for code federation
 * - DllLoader.cs            - load and test dll files
 * - Interfaces.cs           - interface lib for test
 * - MPCommService.cs        - sends and receives messages and files
 * - TestHarnessMessages.cs  - defines the test request message format
 * - Serialization.cs        - serializing and deserializing complex data structures
 * - TestUtilities.cs        - helper functions used mostly for testing
 * 
 * Maintenance History:
 * --------------------
 * Ver 1.0 : 06 Dec 2017
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
  using FileName = String;
  using DirName = String;
  using FileList = List<string>;
  using Msg = CommMessage;

  public struct TestHarnessEnvironment
  {
    public static string storagePath { get; set; } = "../../../TestStorage/";
  }

  ///////////////////////////////////////////////////////////////////
  // TestHarness class
  // - Simulate test harness 

  class TestHarness
  {
    Comm comm_ = null;
    MessageDispatcher dispatcher_ = new MessageDispatcher();

    // Logger
    private StringWriter _LogBuilder;
    public string Log { get { return _LogBuilder.ToString(); } }

    public TestHarness()
    {
      Console.Title = "TestHarness Server";

      ClientEnvironment.fileStorage = TestHarnessEnvironment.storagePath;
      ServiceEnvironment.fileStorage = TestHarnessEnvironment.storagePath;

      string path = Path.GetFullPath(TestHarnessEnvironment.storagePath);
      Console.Write("\n  Test Harness Storage Path: {0}\n", path);

      initializeDispatcher();
    }
    /*----< server finalizer >-------------------------------------*/
    /*
     *  Exception handling is necessary because WCF will throw
     *  an Exception if two servers are started.  That's because
     *  you can only start one listener, per port, per machine.
     */
    ~TestHarness()
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
    /*----< make CommMessage using GUI info >----------------------*/

    CommMessage makeMessage(CommMessage.MessageType msgType)
    {
      CommMessage msg = new CommMessage(msgType);
      msg.from = "http://localhost:8083/IPluggableComm";
      msg.author = "Kaiqi";
      return msg;
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
          int serverPort = 8083;
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
        filesReceived(msg);
        Msg reply = new Msg(Msg.MessageType.noReply);
        return reply;
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
    /*----< request files from child builder and do test >-----------*/

    void processTestRequest(Msg msg)
    {
      Console.Write("\n" + msg.argument);

      // parse test request
      TestRequest request = msg.argument.FromXml<TestRequest>();
      Console.Write("\n123{0}", msg.argument);

      if (request != null) // valid test request got
      {
        // clear old dlls
        //clearTestStorage();

        // get dll from child builder
        Msg reqMsg = new Msg(Msg.MessageType.request);
        reqMsg.to = msg.from;
        reqMsg.from = msg.to;
        reqMsg.command = Msg.Command.sendFile;
        reqMsg.argument = msg.argument; // append test request in file request
        foreach (TestElement te in request.tests)
        {
          string configName = te.buildConfig;
          string dllName = Path.GetFileNameWithoutExtension(configName) + ".dll";
          reqMsg.arguments.Add(dllName);
        }
        comm_.postMessage(reqMsg);

        Console.Write("\n{0}", reqMsg.argument);
      }
    }
    /*----< do tests after dll files received >--------------------*/

    void filesReceived(Msg msg)
    {
      // create a new thread for mock building
      Thread testThrd = new Thread(
        () =>
        {
          // parse test request
          TestRequest request = msg.argument.FromXml<TestRequest>();

          if (request != null) // valid test request got
          {
            Console.Write("\n  Parse test request successfully!");

            foreach (TestElement te in request.tests)
            {
              string configName = te.buildConfig;
              string dllName = Path.GetFileNameWithoutExtension(configName) + ".dll";
              // load dlls and test
              DllLoaderExec loader = new DllLoaderExec();
              DllLoaderExec.testersLocation = Path.GetFullPath(TestHarnessEnvironment.storagePath);
              DllLoaderExec.testFileSpec = Path.GetFullPath(Path.Combine(TestHarnessEnvironment.storagePath, dllName));
              Console.Write("\n  Loading Test Module:\n    {0}\n", DllLoaderExec.testFileSpec);

              // save the original output stream for Console
              TextWriter _old = Console.Out;
              // flush whatever was (if anything) in the log builder
              _LogBuilder = new StringWriter();
              _LogBuilder.Flush();
              Console.SetOut(_LogBuilder);

              // run load and tests
              string result = loader.loadAndExerciseTesters();

              // set the Console output back to its original (StandardOutput that shows on the screen)
              Console.SetOut(_old);

              Console.Write("\n  Test result: {0}\n", result);

              Console.Write("\n  Printing Test Log:");
              Console.Write(Log);

              string logName = saveTestLog(configName, Log);
              sendTestLog(logName);  // send test logs to repo
              notifyClient(msg, configName, result);  // notify client test result
            }
          }
          Console.Write("\n\n  Test finished!\n");
        }
      );
      testThrd.Start();
    }
    /*----< save test log to file >--------------------------------*/

    string saveTestLog(string configName, string log)
    {
      string logName = Path.GetFileNameWithoutExtension(configName)
                               + "-TestLog-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + ".log";
      string logSpec = Path.Combine(TestHarnessEnvironment.storagePath, logName);
      File.WriteAllText(logSpec, log);

      return logName;
    }
    /*----< send test log file to repository >---------------------*/

    void sendTestLog(string logName)
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
    /*----< notify the client with test result >----------------------*/

    void notifyClient(Msg msg, string configName, string result)
    {
      Msg readyMsg = new Msg(Msg.MessageType.noReply);
      readyMsg.to = "http://localhost:8081/IPluggableComm";
      readyMsg.from = msg.to;
      readyMsg.argument = Path.GetFileNameWithoutExtension(configName) + " test: " + result;
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
    //----< delete all files and dirs in the test storage folder >------------

    private bool clearTestStorage()
    {
      try
      {
        DirectoryInfo di = new DirectoryInfo(TestHarnessEnvironment.storagePath);

        foreach (FileInfo file in di.GetFiles())
          file.Delete();
        foreach (DirectoryInfo dir in di.GetDirectories())
          dir.Delete(true);

        return true;
      }
      catch (Exception ex)
      {
        Console.Write("\n--{0}--", ex.Message);
        return false;
      }
    }

    /*----< starts the server process >----------------------------*/

    static void Main(string[] args)
    {
      TestUtilities.title("Starting TestHarness Server", '=');
      TestUtilities.putLine();

      TestHarness server = new TestHarness();
      if (!server.start())
        return;

      Console.Write("\n  Press a key to exit");
      Console.ReadKey();
      TestUtilities.putLine();
    }
  }
}

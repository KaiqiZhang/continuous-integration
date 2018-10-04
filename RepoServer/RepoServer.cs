///////////////////////////////////////////////////////////////////////////
// RepoServer.cs - repository server for code federation                 //
// Ver 1.0                                                               //
// Kaiqi Zhang, Jim Fawcett                                              //
// CSE681 Software Modeling & Analysis, Fall 2017                        //
///////////////////////////////////////////////////////////////////////////
/*
 * Purpose:
 * --------
 * Repository server to store test code and test drivers.
 * 
 * Package Operations:
 * -------------------
 * This package uses WCF for communication. Make sure to run as Administrator
 * because the communication service requires it.
 * It opens a port at 8082.
 * 
 * Required Files:
 * ---------------
 * - RepoServer.cs           - repository server for code federation
 * - MPCommService.cs        - sends and receives messages and files
 * - TestHarnessMessages.cs  - defines the test request message format
 * - Serialization.cs        - serializing and deserializing complex data structures
 * - TestUtilities.cs        - helper functions used mostly for testing
 * 
 * Maintenance History:
 * --------------------
 * Ver 1.1 : 06 Dec 2017
 * - store build requests
 * - store logs
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
  using FileName = String;
  using DirName = String;
  using FileList = List<string>;
  using Msg = CommMessage;

  public struct RepoServerEnvironment
  {
    public static string storagePath { get; set; } = "../../../RepoStorage/";
  }

  ///////////////////////////////////////////////////////////////////
  // RepoServer class
  // - Stores code 

  class RepoServer
  {
    Comm comm_ = null;
    MessageDispatcher dispatcher_ = new MessageDispatcher();

    /*----< initializes server message dispatching >---------------*/

    public RepoServer()
    {
      Console.Title = "Repository Server";

      ClientEnvironment.fileStorage = RepoServerEnvironment.storagePath;
      ServiceEnvironment.fileStorage = RepoServerEnvironment.storagePath;

      string path = Path.GetFullPath(RepoServerEnvironment.storagePath);
      Console.Write("\n  Repo Storage Path: {0}\n", path);

      initializeDispatcher1();
      initializeDispatcher2();
      initializeDispatcher3();
    }
    /*----< server finalizer >-------------------------------------*/
    /*
     *  Exception handling is necessary because WCF will throw
     *  an Exception if two servers are started.  That's because
     *  you can only start one listener, per port, per machine.
     */
    ~RepoServer()
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
      msg.from = "http://localhost:8082/IPluggableComm";
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
          int serverPort = 8082;
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

    void initializeDispatcher1()
    {
      // getFiles
      Func<Msg, Msg> action1 = (Msg msg) =>
      {
        FileList fileList = getFiles(msg.argument, new string[] { ".csproj", ".cs" }, false);
        Msg returnMsg = new Msg(Msg.MessageType.noReply);
        returnMsg.to = msg.from;
        returnMsg.from = msg.to;
        returnMsg.argument = msg.argument;
        foreach (FileName file in fileList)
        {
          Console.Write("\n  file: " + file);
          returnMsg.arguments.Add(file);
        }
        returnMsg.command = msg.command;
        return returnMsg;
      };
      dispatcher_.addCommand(Msg.Command.getFiles, action1);

      // getRequests
      Func<Msg, Msg> action2 = (Msg msg) =>
      {
        FileList fileList = getFiles(msg.argument, new string[] { ".xml" }, true);
        Msg returnMsg = new Msg(Msg.MessageType.noReply);
        returnMsg.to = msg.from;
        returnMsg.from = msg.to;
        returnMsg.argument = msg.argument;
        foreach (FileName file in fileList)
        {
          Console.Write("\n  build request: " + file);
          returnMsg.arguments.Add(file);
        }
        returnMsg.command = msg.command;
        return returnMsg;
      };
      dispatcher_.addCommand(Msg.Command.getRequests, action2);

      
    }
    /*----< here server responses to messages are defined >--------*/

    void initializeDispatcher2()
    {
      // getLogs
      Func<Msg, Msg> action3 = (Msg msg) =>
      {
        FileList fileList = getFiles(msg.argument, new string[] { ".log" }, true);
        Msg returnMsg = new Msg(Msg.MessageType.noReply);
        returnMsg.to = msg.from;
        returnMsg.from = msg.to;
        returnMsg.argument = msg.argument;
        foreach (FileName file in fileList)
        {
          Console.Write("\n  log: " + file);
          returnMsg.arguments.Add(file);
        }
        returnMsg.command = msg.command;
        return returnMsg;
      };
      dispatcher_.addCommand(Msg.Command.getLogs, action3);

      // testRequest
      Func<Msg, Msg> action4 = (Msg msg) =>
      {
        if (msg.type == Msg.MessageType.request)
        {
          string xmlRequest = msg.argument;
          saveTestRequest(xmlRequest);
          processTestRequest(xmlRequest);
        }
        Msg returnMsg = new Msg(Msg.MessageType.noReply);
        returnMsg.to = msg.from;
        returnMsg.from = msg.to;
        returnMsg.argument = msg.argument;
        returnMsg.command = msg.command;
        return returnMsg;
      };
      dispatcher_.addCommand(Msg.Command.testRequest, action4);
    }
    /*----< here server responses to messages are defined >--------*/

    void initializeDispatcher3()
    {
      // testRequestOnRepo
      Func<Msg, Msg> action5 = (Msg msg) =>
      {
        if (msg.type == Msg.MessageType.request)
        {
          string fileName = msg.argument;
          string path = Path.Combine(RepoServerEnvironment.storagePath, fileName);
          string xmlRequest = File.ReadAllText(path);
          processTestRequest(xmlRequest);
        }
        Msg returnMsg = new Msg(Msg.MessageType.noReply);
        returnMsg.to = msg.from;
        returnMsg.from = msg.to;
        returnMsg.argument = msg.argument;
        returnMsg.command = msg.command;
        return returnMsg;
      };
      dispatcher_.addCommand(Msg.Command.testRequestOnRepo, action5);

      // sendFile
      Func<Msg, Msg> action6 = (Msg msg) =>
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
      };
      dispatcher_.addCommand(Msg.Command.sendFile, action6);
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
    /*----< returns names of files stored in server folder >-------*/

    public static FileList getFiles(DirName category, string[] patterns, bool sortByTime)
    {
      FileList files = new FileList();
      try
      {
        string path = System.IO.Path.Combine(RepoServerEnvironment.storagePath + "/", category);

        IEnumerable<FileInfo> fileInfos;
        if (sortByTime)
          fileInfos = new DirectoryInfo(path).GetFiles().OrderByDescending(f => f.LastWriteTime);
        else
          fileInfos = new DirectoryInfo(path).GetFiles();

        if (patterns == null || patterns.Length == 0)
          files = fileInfos.Select(x => x.Name).ToList<string>();
        else
          files = fileInfos.Select(x => x.Name).Where(f => patterns.Any(f.ToLower().EndsWith)).ToList<string>();

        for (int i = 0; i < files.Count; ++i)
        {
          files[i] = System.IO.Path.GetFileName(files[i]);
        }
        return files;
      }
      catch
      {
        return files;
      }
    }
    /*----< store build request as a xml file on repo >------------*/

    void saveTestRequest(String xmlRequest)
    {
      String fileName = "BuildRequest-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + ".xml";
      string path = Path.Combine(RepoServerEnvironment.storagePath, fileName);
      File.WriteAllText(path, xmlRequest);
    }
    /*----< send test request to mother builder server >----------*/

    void processTestRequest(String xmlRequest)
    {
      Console.Write("\n" + xmlRequest);

      Msg builderMsg = makeMessage(Msg.MessageType.request);
      builderMsg.to = "http://localhost:8080/IPluggableComm";
      builderMsg.argument = xmlRequest;
      builderMsg.command = Msg.Command.testRequest;

      try
      {
        createCommIfNeeded();
        comm_.postMessage(builderMsg);
      }
      catch
      {
        Console.Write("\n  Error: can't connect");
      }
    }
    /*----< starts the server process >----------------------------*/

    static void Main(string[] args)
    {
      TestUtilities.title("Starting Repository Server", '=');
      TestUtilities.putLine();

      RepoServer server = new RepoServer();
      if (!server.start())
        return;

      Console.Write("\n  Press a key to exit");
      Console.ReadKey();
      TestUtilities.putLine();
    }
  }
}

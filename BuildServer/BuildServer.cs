///////////////////////////////////////////////////////////////////////////
// BuildServer.cs - Mother builder server for code federation            //
// Ver 1.0                                                               //
// Kaiqi Zhang, Jim Fawcett                                              //
// CSE681 Software Modeling & Analysis, Fall 2017                        //
///////////////////////////////////////////////////////////////////////////
/*
 * Purpose:
 * --------
 * Mother build server for code federation. This application can create a
 * process pool with specified number of child builder processes. It checks
 * if there're available child builders and send test request to them.
 * 
 * Package Operations:
 * -------------------
 * This package uses WCF for communication. Make sure to run as Administrator
 * because the communication service requires it.
 * It opens a port at 8080.
 * 
 * Required Files:
 * ---------------
 * - BuildServer.cs          - GUI for start builder, send request
 * - MPCommService.cs        - sends and receives messages and files
 * - TestHarnessMessages.cs  - defines the test request message format
 * - Serialization.cs        - serializing and deserializing complex data structures
 * - TestUtilities.cs        - helper functions used mostly for testing
 * - ChildBuilder.exe        - pre compiled childer builder program
 * 
 * Maintenance History:
 * --------------------
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
using System.Diagnostics;

using MessagePassingComm;

namespace RemoteBuildServer
{
  using Msg = CommMessage;

  ///////////////////////////////////////////////////////////////////
  // BuildServer class
  // Mother builder of the builder server

  class BuildServer
  {
    ///////////////////////////////////////////////////////////////////
    // ChildBuilder class
    // Store child process status

    class ChildNode
    {
      public Process process { set; get; }
      public bool ready { set; get; }
      public string address { set; get; }

      public ChildNode(Process proc, int id)
      {
        this.process = proc;
        this.ready = true;
        this.address = "http://localhost:" + (8090 + id) + "/IPluggableComm";
      }

      public void setBusy() { ready = false; }
      public void setReady() { ready = true; }
    }

    Comm comm_ = null;
    MessageDispatcher dispatcher_ = new MessageDispatcher();
    List<ChildNode> childs_ = new List<ChildNode>();
    SWTools.BlockingQueue<TestRequest> requestQ_ = new SWTools.BlockingQueue<TestRequest>();
    SWTools.BlockingQueue<int> readyQ_ = new SWTools.BlockingQueue<int>();

    int maxChildsNum = 10;

    /*----< initializes server message dispatching >---------------*/

    public BuildServer()
    {
      Console.Title = "Mother Build Server";
      initializeDispatcher();
    }
    /*----< server finalizer >-------------------------------------*/
    /*
     *  Exception handling is necessary because WCF will throw
     *  an Exception if two servers are started.  That's because
     *  you can only start one listener, per port, per machine.
     */
    ~BuildServer()
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
    /*----< create a child process with index number >------------*/

    bool createProcess(int i)
    {
      string fileName = "..\\..\\..\\ChildBuilder\\bin\\Debug\\ChildBuilder.exe";
      string absFileSpec = Path.GetFullPath(fileName);
      Console.Write("\n  attempting to start {0}", absFileSpec);
      string commandline = i.ToString();
      try
      {
        Process proc = Process.Start(fileName, commandline);
        ChildNode child = new ChildNode(proc, i);
        childs_.Add(child);
        readyQ_.enQ(i);
      }
      catch (Exception ex)
      {
        Console.Write("\n  {0}", ex.Message);
        return false;
      }
      return true;
    }
    /*----< start a process pool with specified number of processes >----*/

    bool startPool(int n)
    {
      if (childs_.Count > 0)
      {
        Console.Write("  Please shutdown process pool first");
        return false;
      }

      for (int i = 0; i < n; i++)
      {
        if (createProcess(i))
        {
          Console.Write(" - succeeded");
        }
        else
        {
          Console.Write(" - failed");
          return false;
        }
      }
      return true;
    }
    /*----< kill all child processes in the pool >-----------------*/

    bool shutdownPool()
    {
      for (int i = 0; i < childs_.Count; i++)
      {
        // kill process
        Console.Write("\n  kill child process: " + (i+1));
        childs_[i].process.Kill();
      }
      childs_.Clear();
      readyQ_.clear();
      return true;
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
          int serverPort = 8080;
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
      initializeDispatcherPool();
      initializeDispatcherRequest();
      initializeDispatcherReady();
    }
    /*----< initialize dispatcher for start and shutdown process pool msg >----*/

    void initializeDispatcherPool()
    {
      // start pool
      Func<Msg, Msg> action1 = (Msg msg) =>
      {
        int processNum = 0;
        if (int.TryParse(msg.argument, out processNum))
        {
          if (processNum < 0) processNum = 0;
          if (processNum > maxChildsNum) processNum = maxChildsNum;
          startPool(processNum);
        }
        Msg returnMsg = new Msg(Msg.MessageType.noReply);
        returnMsg.to = msg.from;
        returnMsg.from = msg.to;
        returnMsg.command = msg.command;
        return returnMsg;
      };
      dispatcher_.addCommand(Msg.Command.startProcessPool, action1);

      // shutdown pool
      Func<Msg, Msg> action2 = (Msg msg) =>
      {
        shutdownPool();
        Msg returnMsg = new Msg(Msg.MessageType.noReply);
        returnMsg.to = msg.from;
        returnMsg.from = msg.to;
        returnMsg.command = msg.command;
        return returnMsg;
      };
      dispatcher_.addCommand(Msg.Command.closeProcessPool, action2);
    }
    /*----< initialize dispatcher for test request msg >-------------*/

    void initializeDispatcherRequest()
    {
      // testRequest
      Func<Msg, Msg> action3 = (Msg msg) =>
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
      dispatcher_.addCommand(Msg.Command.testRequest, action3);
    }
    /*----< initialize dispatcher for ready request >---------------*/

    void initializeDispatcherReady()
    {
      // ready from child
      Func<Msg, Msg> action4 = (Msg msg) =>
      {
        Console.Write("\n  Child #{0} ready!", msg.argument);
        readyQ_.enQ(int.Parse(msg.argument));
        Msg returnMsg = new Msg(Msg.MessageType.noReply);
        returnMsg.to = msg.from;
        returnMsg.from = msg.to;
        returnMsg.argument = msg.argument;
        returnMsg.command = msg.command;
        return returnMsg;
      };
      dispatcher_.addCommand(Msg.Command.ready, action4);
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

        Thread childThrd = new Thread(threadProc2);
        childThrd.IsBackground = true;
        childThrd.Start();
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
    /*----< dequeue a test request, wait for a ready child to process >----*/

    void threadProc2()
    {
      while (true)
      {
        TestRequest request = requestQ_.deQ();
        try
        {
          int i = readyQ_.deQ();
          Console.Write("\n  Polled from request queue and send to child {0}", i);
          Msg childMsg = new Msg(Msg.MessageType.request);
          childMsg.to = childs_[i].address;
          childMsg.from = "http://localhost:8080/IPluggableComm";
          childMsg.argument = request.ToXml();
          childMsg.command = Msg.Command.testRequest;

          try
          {
            createCommIfNeeded();
            comm_.postMessage(childMsg);
          }
          catch
          {
            Console.Write("\n  Error: can't connect");
          }

          childs_[i].setBusy();
        }
        catch
        {
          Console.Write("\n  Error: child builder doesn't exist");
        }
      }
    }
    /*----< enqueue test request to a queue >----------------------*/

    void processTestRequest(Msg msg)
    {
      Console.Write("\n" + msg.argument);
      // parse test request
      TestRequest request = msg.argument.FromXml<TestRequest>();

      requestQ_.enQ(request);

      Console.Write("\n  Inserted into request queue");
    }
    /*----< starts the server process >----------------------------*/

    static void Main(string[] args)
    {
      TestUtilities.title("Starting Builder Server", '=');
      TestUtilities.putLine();

      BuildServer server = new BuildServer();
      if (!server.start())
      {
        Console.Write("\n");
        return;
      }

      Console.Write("\n  Press a key to exit");
      Console.ReadKey();
      TestUtilities.putLine();
    }
  }
}

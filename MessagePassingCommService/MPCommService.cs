/////////////////////////////////////////////////////////////////////
// MPCommService.cs - service for MessagePassingComm               //
// ver 2.1                                                         //
// Jim Fawcett, CSE681-OnLine, Summer 2017                         //
/////////////////////////////////////////////////////////////////////
/*
 * Started this project with C# Console Project wizard
 * - Added references to:
 *   - System.ServiceModel
 *   - System.Runtime.Serialization
 *   - System.Threading;
 *   - System.IO;
 *   
 * Package Operations:
 * -------------------
 * This package defines three classes:
 * - Sender which implements the public methods:
 *   -------------------------------------------
 *   - connect          : opens channel and attempts to connect to an endpoint, 
 *                        trying multiple times to send a connect message
 *   - close            : closes channel
 *   - postMessage      : posts to an internal thread-safe blocking queue, which
 *                        a sendThread then dequeues msg, inspects for destination,
 *                        and calls connect(address, port)
 *   - postFile         : attempts to upload a file in blocks
 *   - getLastError     : returns exception messages on method failure
 * - Receiver which implements the public methods:
 *   ---------------------------------------------
 *   - start            : creates instance of ServiceHost which services incoming messages
 *   - postMessage      : Sender proxies call this message to enqueue for processing
 *   - getMessage       : called by Receiver application to retrieve incoming messages
 *   - close            : closes ServiceHost
 *   - openFileForWrite : opens a file for storing incoming file blocks
 *   - writeFileBlock   : writes an incoming file block to storage
 *   - closeFile        : closes newly uploaded file
 * - Comm which implements, using Sender and Receiver instances, the public methods:
 *   -------------------------------------------------------------------------------
 *   - postMessage      : send CommMessage instance to a Receiver instance
 *   - getMessage       : retrieves a CommMessage from a Sender instance
 *   - postFile         : called by a Sender instance to transfer a file
 *    
 * The Package also implements the class TestPCommService with public methods:
 * ---------------------------------------------------------------------------
 * - testSndrRcvr()     : test instances of Sender and Receiver
 * - testComm()         : test Comm instance
 * - compareMsgs        : compare two CommMessage instances for near equality
 * - compareFileBytes   : compare two files byte by byte
 *
 * Required Files:
 * ---------------
 * IMPCommService.cs, MPCommService.cs
 * 
 * Maintenance History:
 * --------------------
 * ver 2.1 : 20 Oct 2017
 * - minor changes to these comments
 * ver 2.0 : 19 Oct 2017
 * - renamed namespace and several components
 * - eliminated IPluggable.cs
 * ver 1.0 : 14 Jun 2017
 * - first release
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Threading;
using System.IO;

namespace MessagePassingComm
{
  using Msg = CommMessage;

  ///////////////////////////////////////////////////////////////////
  // MessageDispatcher class
  // - calls stored lambdas in response to received message

  public class MessageDispatcher
  {
    Dictionary<Msg.Command, Func<Msg, Msg>> dispather = new Dictionary<Msg.Command, Func<Msg, Msg>>();

    /*----< add lambda, bound to Func delegate, keyed to msg >-----*/

    public void addCommand(Msg.Command cmd, Func<Msg, Msg> action)
    {
      dispather[cmd] = action;
    }
    /*----< invokes delegate bound to cmd key >--------------------*/
    /*
     *  returns procError message if cmd doesn't have a bound lambda
     */
    public Msg doCommand(Msg.Command cmd, Msg msg)
    {
      if (dispather.Keys.Contains(cmd))
        return dispather[cmd].Invoke(msg);
      else
      {
        Msg reply = new Msg(Msg.MessageType.procError);
        reply.to = msg.from;
        reply.from = msg.to;
        reply.command = msg.command;
        return reply;
      }
    }
  }

  ///////////////////////////////////////////////////////////////////
  // Receiver class - receives CommMessages and Files from Senders

  public class Receiver : IMessagePassingComm
  {
    public static SWTools.BlockingQueue<CommMessage> rcvQ { get; set; } = null;
    ServiceHost commHost = null;
    FileStream fs = null;
    string lastError = "";

    /*----< constructor >------------------------------------------*/

    public Receiver()
    {
      if (rcvQ == null)
        rcvQ = new SWTools.BlockingQueue<CommMessage>();
    }
    /*----< create ServiceHost listening on specified endpoint >---*/
    /*
     * baseAddress is of the form: http://IPaddress or http://networkName
     */
    public void start(string baseAddress, int port)
    {  
      string address = baseAddress + ":" + port.ToString() + "/IPluggableComm";
      TestUtilities.putLine(string.Format("starting Receiver on thread {0}", Thread.CurrentThread.ManagedThreadId));
      createCommHost(address);
    }
    /*----< create ServiceHost listening on specified endpoint >---*/
    /*
     * address is of the form: http://IPaddress:8080/IPluggableComm
     */
    public void createCommHost(string address)
    {
      WSHttpBinding binding = new WSHttpBinding();
      Uri baseAddress = new Uri(address);
      commHost = new ServiceHost(typeof(Receiver), baseAddress);
      commHost.AddServiceEndpoint(typeof(IMessagePassingComm), binding, baseAddress);
      commHost.Open();
    }
    /*----< enqueue a message for transmission to a Receiver >-----*/

    public void postMessage(CommMessage msg)
    {
      msg.threadId = Thread.CurrentThread.ManagedThreadId;
      TestUtilities.putLine(string.Format("sender enqueuing message on thread {0}", Thread.CurrentThread.ManagedThreadId));
      rcvQ.enQ(msg);
    }
    /*----< retrieve a message sent by a Sender instance >---------*/

    public CommMessage getMessage()
    {
      CommMessage msg = rcvQ.deQ();
      if (msg.type == CommMessage.MessageType.closeReceiver)
      {
        close();
      }
      return msg;
    }
    /*----< close ServiceHost >------------------------------------*/

    public void close()
    {
      TestUtilities.putLine("closing receiver - please wait");
      //Console.Out.Flush();
      commHost.Close();
    }
    /*---< called by Sender's proxy to open file on Receiver >-----*/

    public bool openFileForWrite(string name)
    {
      try
      {
        string writePath = Path.Combine(ServiceEnvironment.fileStorage, name);
        fs = File.OpenWrite(writePath);
        return true;
      }
      catch(Exception ex)
      {
        lastError = ex.Message;
        return false;
      }
    }
    /*----< write a block received from Sender instance >----------*/

    public bool writeFileBlock(byte[] block)
    {
      try
      {
        fs.Write(block, 0, block.Length);
        return true;
      }
      catch (Exception ex)
      {
        lastError = ex.Message;
        return false;
      }
    }
    /*----< close Receiver's uploaded file >-----------------------*/

    public void closeFile()
    {
      fs.Close();
    }
  }
  ///////////////////////////////////////////////////////////////////
  // Sender class - sends messages and files to Receiver

  public class Sender
  {
    private IMessagePassingComm channel;
    private ChannelFactory<IMessagePassingComm> factory = null;
    private SWTools.BlockingQueue<CommMessage> sndQ = null;
    private int port = 0;
    private string fromAddress = "";
    private string toAddress = "";
    Thread sndThread = null;
    int tryCount = 0, maxCount = 10;
    string lastError = "";
    //string lastUrl = "";

    /*----< constructor >------------------------------------------*/

    public Sender(string baseAddress, int listenPort)
    {
      port = listenPort;
      fromAddress = baseAddress + listenPort.ToString() + "/IMessagePassingComm";
      sndQ = new SWTools.BlockingQueue<CommMessage>();
      TestUtilities.putLine(string.Format("starting Sender on thread {0}", Thread.CurrentThread.ManagedThreadId));
      sndThread = new Thread(threadProc);
      sndThread.Start();
    }
    /*----< creates proxy with interface of remote instance >------*/

    public void createSendChannel(string address)
    {
      EndpointAddress baseAddress = new EndpointAddress(address);
      WSHttpBinding binding = new WSHttpBinding();
      factory = new ChannelFactory<IMessagePassingComm>(binding, address);
      channel = factory.CreateChannel();
    }
    /*----< attempts to connect to Receiver instance >-------------*/

    public bool connect(string baseAddress, int port)
    {
      toAddress = baseAddress + ":" + port.ToString() + "/IPluggableComm";
      return connect(toAddress);
    }
    /*----< attempts to connect to Receiver instance >-------------*/
    /*
     * - attempts a finite number of times to connect to a Receiver
     * - first attempt to send will throw exception of no listener
     *   at the specified endpoint
     * - to test that we attempt to send a connect message
     */
    public bool connect(string toAddress)
    {
      int timeToSleep = 500;
      try
      {
        createSendChannel(toAddress);
      }
      catch
      {
        Console.Write("\n  can't create channel");
      }
      CommMessage connectMsg = new CommMessage(CommMessage.MessageType.connect);
      while (true)
      {
        try
        {
          channel.postMessage(connectMsg);
          tryCount = 0;
          return true;
        }
        catch (Exception ex)
        {
          if (++tryCount < maxCount)
          {
            TestUtilities.putLine("failed to connect - waiting to try again");
            Thread.Sleep(timeToSleep);
          }
          else
          {
            TestUtilities.putLine("failed to connect - quitting");
            lastError = ex.Message;
            return false;
          }
        }
      }
    }
    /*----< closes Sender's proxy >--------------------------------*/

    public void close()
    {
      try
      {
        if (factory != null)
          factory.Close();
      }
      catch (Exception ex)
      {
        Console.Write("\n  An error occured while trying to build the xml file.\n  Details: {0}\n", ex.Message);
      }
    }
    /*----< processing for receive thread >------------------------*/
    /*
     * - send thread dequeues send message and posts to channel proxy
     * - thread inspects message and routes to appropriate specified endpoint
     */
    void threadProc()
    {
      while(true)
      {
        TestUtilities.putLine(string.Format("sender enqueuing message on thread {0}", Thread.CurrentThread.ManagedThreadId));

        CommMessage msg = sndQ.deQ();
        if (msg.type == CommMessage.MessageType.closeSender)
        {
          TestUtilities.putLine("Sender send thread quitting");
          break;
        }

        close();
        if (!connect(msg.to))
          continue;
        
        try
        {
          channel.postMessage(msg);
        }
        catch
        {
          Console.Write("\n  Error: post message failed");
        }
        close();
      }
    }
    /*----< main thread enqueues message for sending >-------------*/

    public void postMessage(CommMessage msg)
    {
      sndQ.enQ(msg);
    }
    /*----< uploads file to Receiver instance >--------------------*/

    public bool postFile(string toAddress, string fileName)
    {
      FileStream fs = null;
      long bytesRemaining;

      close();
      if (!connect(toAddress))
        return false;

      try
      {
        string path = Path.Combine(ClientEnvironment.fileStorage, fileName);
        fs = File.OpenRead(path);
        bytesRemaining = fs.Length;
        channel.openFileForWrite(fileName);
        while (true)
        {
          long bytesToRead = Math.Min(ClientEnvironment.blockSize, bytesRemaining);
          byte[] blk = new byte[bytesToRead];
          long numBytesRead = fs.Read(blk, 0, (int)bytesToRead);
          bytesRemaining -= numBytesRead;

          channel.writeFileBlock(blk);
          if (bytesRemaining <= 0)
            break;
        }
        channel.closeFile();
        fs.Close();
        close();
      }
      catch (Exception ex)
      {
        lastError = ex.Message;
        close();
        return false;
      }
      return true;
    }
  }
  ///////////////////////////////////////////////////////////////////
  // Comm class combines Receiver and Sender

  public class Comm
  {
    private Receiver rcvr = null;
    private Dictionary<string, Sender> sndrs = null;
    private string baseAddress;
    private int port;

    /*----< constructor >------------------------------------------*/
    /*
     * - starts listener listening on specified endpoint
     */
    public Comm(string baseAddress, int port)
    {
      this.baseAddress = baseAddress;
      this.port = port;
      rcvr = new Receiver();
      rcvr.start(baseAddress, port);
      //sndr = new Sender(baseAddress, port);
      sndrs = new Dictionary<string, Sender>();
    }
    /*----< post message to remote Comm >--------------------------*/

    public void postMessage(CommMessage msg)
    {
      if (msg.to == null) return;

      if (!sndrs.ContainsKey(msg.to))
        sndrs[msg.to] = new Sender(baseAddress, port);

      sndrs[msg.to].postMessage(msg);
    }
    /*----< retrieve message from remote Comm >--------------------*/

    public CommMessage getMessage()
    {
      return rcvr.getMessage();
    }
    /*----< called by remote Comm to upload file >-----------------*/

    public bool postFile(string toAddress, string filename)
    {
      if (toAddress == null) return false;

      if (!sndrs.ContainsKey(toAddress))
        sndrs[toAddress] = new Sender(baseAddress, port);

      return sndrs[toAddress].postFile(toAddress, filename);
    }
    /*----< disconnect sender >------------------------------------*/

    public void disconnect()
    {
      foreach (string key in sndrs.Keys)
        sndrs[key].close();
    }
    /*----< disconnect sender >------------------------------------*/

    public void disconnect(string toAddress)
    {
      if (sndrs.ContainsKey(toAddress))
        sndrs[toAddress].close();
    }
    /*----< remove sender >--------------------------------------*/

    public void removeSender(string toAddress)
    {
      sndrs.Remove(toAddress);
    }
    /*----< shutdown listener >------------------------------------*/

    public void stopListener()
    {
      rcvr.close();
    }
    /*----< close the communication channel >----------------------*/

    public void close()
    {
      CommMessage msg = new CommMessage(CommMessage.MessageType.closeReceiver);
      if (rcvr != null)
      {
        postMessage(msg);
      }
      if (sndrs.Count > 0)
      {
        msg.type = CommMessage.MessageType.closeSender;
        postMessage(msg);
      }
    }
  }
  ///////////////////////////////////////////////////////////////////
  // TestPCommService class - tests Receiver, Sender, and Comm

  class TestPCommService
  {
    /*----< collect file names from client's FileStore >-----------*/

    public static List<string> getClientFileList()
    {
      List<string> names = new List<string>();
      string[] files = Directory.GetFiles(ClientEnvironment.fileStorage);
      foreach(string file in files)
      {
        names.Add(Path.GetFileName(file));
      }
      return names;
    }
    /*----< compare CommMessages property by property >------------*/
    /*
     * - skips threadId property
     */
    public static bool compareMsgs(CommMessage msg1, CommMessage msg2)
    {
      bool t1 = (msg1.type == msg2.type);
      bool t2 = (msg1.to == msg2.to);
      bool t3 = (msg1.from == msg2.from);
      bool t4 = (msg1.author == msg2.author);
      bool t5 = (msg1.command == msg2.command);
      //bool t6 = (msg1.threadId == msg2.threadId);
      bool t7 = (msg1.errorMsg == msg2.errorMsg);
      if (msg1.arguments.Count != msg2.arguments.Count)
        return false;
      for(int i=0; i<msg1.arguments.Count; ++i)
      {
        if (msg1.arguments[i] != msg2.arguments[i])
          return false;
      }
      return t1 && t2 && t3 && t4 && t5 && /*t6 &&*/ t7;
    }
    /*----< compare binary file's bytes >--------------------------*/

    static bool compareFileBytes(string filename)
    {
      TestUtilities.putLine(string.Format("testing byte equality for \"{0}\"", filename));

      string fileSpec1 = Path.Combine(ClientEnvironment.fileStorage, filename);
      string fileSpec2 = Path.Combine(ServiceEnvironment.fileStorage, filename);
      try
      {
        byte[] bytes1 = File.ReadAllBytes(fileSpec1);
        byte[] bytes2 = File.ReadAllBytes(fileSpec2);
        if (bytes1.Length != bytes2.Length)
          return false;
        for (int i = 0; i < bytes1.Length; ++i)
        {
          if (bytes1[i] != bytes2[i])
            return false;
        }
      }
      catch (Exception ex)
      {
        TestUtilities.putLine(string.Format("\n  {0}\n", ex.Message));
        return false;
      }
      return true;
    }
    /*----< test Sender and Receiver classes >---------------------*/

    public static bool testSndrRcvr()
    {
      TestUtilities.vbtitle("testing Sender & Receiver");

      bool test = true;
      Receiver rcvr = new Receiver();
      rcvr.start("http://localhost", 8080);
      Sender sndr = new Sender("http://localhost", 8080);

      CommMessage sndMsg = new CommMessage(CommMessage.MessageType.request);
      sndMsg.command = Msg.Command.show;
      sndMsg.author = "Jim Fawcett";
      sndMsg.to = "http://localhost:8080/IPluggableComm";
      sndMsg.from = "http://localhost:8080/IPluggableComm";

      sndr.postMessage(sndMsg);
      CommMessage rcvMsg;
      // get connection message
      rcvMsg = rcvr.getMessage();
      if (ClientEnvironment.verbose)
        rcvMsg.show();
      // get first info message
      rcvMsg = rcvr.getMessage();
      if (ClientEnvironment.verbose)
        rcvMsg.show();
      if (!compareMsgs(sndMsg, rcvMsg))
        test = false;
      TestUtilities.checkResult(test, "sndMsg equals rcvMsg");
      TestUtilities.putLine();

      sndMsg.type = CommMessage.MessageType.closeReceiver;
      sndr.postMessage(sndMsg);
      rcvMsg = rcvr.getMessage();
      if (ClientEnvironment.verbose)
        rcvMsg.show();
      if (!compareMsgs(sndMsg, rcvMsg))
        test = false;
      TestUtilities.checkResult(test, "Close Receiver");
      TestUtilities.putLine();

      sndMsg.type = CommMessage.MessageType.closeSender;
      if (ClientEnvironment.verbose)
        sndMsg.show();
      sndr.postMessage(sndMsg);
      // rcvr.getMessage() would fail because server has shut down
      // no rcvMsg so no compare

      TestUtilities.putLine("last message received\n");
      return test;
    }
    /*----< test Comm instance >-----------------------------------*/

    public static bool testComm()
    {
      TestUtilities.vbtitle("testing Comm");
      bool test = true;

      Comm comm = new Comm("http://localhost", 8081);
      CommMessage csndMsg = new CommMessage(CommMessage.MessageType.request);

      csndMsg.command = Msg.Command.show;
      csndMsg.author = "Jim Fawcett";
      csndMsg.to = "http://localhost:8081/IPluggableComm";
      csndMsg.from = "http://localhost:8081/IPluggableComm";

      comm.postMessage(csndMsg);
      CommMessage crcvMsg = comm.getMessage();
      if (ClientEnvironment.verbose)
        crcvMsg.show();

      crcvMsg = comm.getMessage();
      if (ClientEnvironment.verbose)
        crcvMsg.show();
      if (!compareMsgs(csndMsg, crcvMsg))
        test = false;
      TestUtilities.checkResult(test, "csndMsg equals crcvMsg");
      TestUtilities.putLine();

      bool testFileTransferResult = testFileTransfer(comm, csndMsg);

      TestUtilities.vbtitle("test receiver close");
      csndMsg.type = CommMessage.MessageType.closeReceiver;
      comm.postMessage(csndMsg);
      crcvMsg = comm.getMessage();
      if (ClientEnvironment.verbose)
        crcvMsg.show();
      if (!compareMsgs(csndMsg, crcvMsg))
        test = false;
      TestUtilities.checkResult(test, "closeReceiver");
      TestUtilities.putLine();

      csndMsg.type = CommMessage.MessageType.closeSender;
      comm.postMessage(csndMsg);
      if(ClientEnvironment.verbose)
        csndMsg.show();
      // comm.getMessage() would fail because server has shut down
      // no rcvMsg so no compare

      TestUtilities.putLine("last message received\n");

      return test && testFileTransferResult;
    }

    private static bool testFileTransfer(Comm comm, CommMessage csndMsg)
    {
      TestUtilities.vbtitle("testing file transfer");

      bool result = true;

      List<string> names = getClientFileList();
      foreach (string name in names)
      {
        TestUtilities.putLine(string.Format("transferring file \"{0}\"", name));
        bool transferSuccess = comm.postFile(csndMsg.to, name);
        TestUtilities.checkResult(transferSuccess, "transfer");
      }

      foreach (string name in names)
      {
        if (!compareFileBytes(name))
        {
          result = false;
          break;
        }
      }
      TestUtilities.checkResult(result, "file transfers");
      TestUtilities.putLine();

      return result;
    }
    /*----< do the tests >-----------------------------------------*/

    static void Main(string[] args)
    {
      ClientEnvironment.verbose = true;
      TestUtilities.vbtitle("testing Message-Passing Communication", '=');

      /*----< uncomment to see Sender & Receiver testing >---------*/
      //TestUtilities.checkResult(testSndrRcvr(), "Sender & Receiver");
      //TestUtilities.putLine();

      TestUtilities.checkResult(testComm(), "Comm");
      TestUtilities.putLine();

      TestUtilities.putLine("Press key to quit\n");
      if(ClientEnvironment.verbose)
        Console.ReadKey();
    }
  }
}

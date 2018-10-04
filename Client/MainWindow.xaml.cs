///////////////////////////////////////////////////////////////////////////
// MainWindow.xaml.cs - Client GUI for code federation                   //
// Ver 1.0                                                               //
// Kaiqi Zhang, Jim Fawcett                                              //
// CSE681 Software Modeling & Analysis, Fall 2017                        //
///////////////////////////////////////////////////////////////////////////
/*
 * Purpose:
 * --------
 * GUI client for code federation. This application can start or shutdown
 * process pool via WCF messages, and send test request to repository server.
 * 
 * Package Operations:
 * -------------------
 * This package uses WCF for communication. Make sure to run as Administrator
 * because the communication service requires it.
 * It opens a port at 8081.
 * 
 * Required Files:
 * ---------------
 * - MainWindow.xaml, MainWindow.xaml.cs - GUI for start builder, send request
 * - MPCommService.cs        - sends and receives messages and files
 * - TestHarnessMessages.cs  - defines the test request message format
 * - Serialization.cs        - serializing and deserializing complex data structures
 * - TestUtilities.cs        - helper functions used mostly for testing
 * 
 * Maintenance History:
 * --------------------
 * Ver 1.1 : 06 Dec 2017
 * - add functions for project 4
 * Ver 1.0 : 29 Oct 2017
 * - first release
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO;

using MessagePassingComm;

namespace RemoteBuildServer
{
  /// // aliases with semantic meaning
  using Msg = CommMessage;

  public struct ClientEnvironment
  {
    public static string storagePath { get; set; } = "../../../ClientStorage/";
  }

  ///////////////////////////////////////////////////////////////////
  // MainWindow class
  //   - provides display for all client functions

  public partial class MainWindow : Window
  {
    Comm comm_ = null;
    Thread rcvThrd = null;
    Thread tTest = null;
    MessageDispatcher dispatcher_ = new MessageDispatcher();
    TestRequest tr = null;
    List<Window1> popups = new List<Window1>();

    int maxInMsgCount = 50;

    /*----< constructor >------------------------------------------*/

    public MainWindow()
    {
      Console.Title = "GUI Client";
      InitializeComponent();

      // set storage directory
      ServiceEnvironment.fileStorage = ClientEnvironment.storagePath;

      string path = System.IO.Path.GetFullPath(ServiceEnvironment.fileStorage);
      Console.Write("\n  Client Storage Path: {0}\n", path);
      if (!Directory.Exists(path))
        Directory.CreateDirectory(path);
    }
    /*----< start Repository and initialize views >----------------*/

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      initializeDispatcher1();
      initializeDispatcher2();
      btnResetRequest_Click(null, null);

      rcvThrd = new Thread(
        () => rcvProc("http://localhost", "8081")
      );
      rcvThrd.Start();

      // run demonstration
      tTest = new Thread(
        () =>
        {
          Thread.Sleep(1000);
          // do test executive
          TestExecutive exec = new TestExecutive();
          exec.DemoReq(this);
        }
      );
      tTest.Start();
    }
    /*----< close any open popup code view windows >---------------*/

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      foreach (var popup in popups)
        popup.Close();
      if (comm_ != null)
        comm_.close();
    }
    /*----< bind client processing to message types >--------------*/
    /*
     *  This is where we determine how incoming messages are
     *  processed by client.
     */
    void initializeDispatcher1()
    {
      // getFiles
      Func<Msg, Msg> action1 = (Msg msg) =>
      {
        fileListBox.Items.Clear();
        foreach (string file in msg.arguments)
          fileListBox.Items.Add(file);
        Msg reply = new Msg(Msg.MessageType.noReply);
        return reply;
      };
      dispatcher_.addCommand(Msg.Command.getFiles, action1);

      // getRequests
      Func<Msg, Msg> action2 = (Msg msg) =>
      {
        requestListBox.Items.Clear();
        foreach (string file in msg.arguments)
          requestListBox.Items.Add(file);
        Msg reply = new Msg(Msg.MessageType.noReply);
        return reply;
      };
      dispatcher_.addCommand(Msg.Command.getRequests, action2);

      // getLogs
      Func<Msg, Msg> action3 = (Msg msg) =>
      {
        logListBox.Items.Clear();
        foreach (string file in msg.arguments)
          logListBox.Items.Add(file);
        Msg reply = new Msg(Msg.MessageType.noReply);
        return reply;
      };
      dispatcher_.addCommand(Msg.Command.getLogs, action3);

      // send file
      Func<Msg, Msg> action4 = (Msg msg) =>
      {
        string fileName = msg.arguments[0];
        Console.Write("\n  received file: {0}", fileName);
        // double click on file name so show its text
        Window1 codePopup = new Window1();
        codePopup.Show();
        popups.Add(codePopup);
        codePopup.codeLabel.Content = fileName;
        showFile(fileName, codePopup);
        Msg reply = new Msg(Msg.MessageType.noReply);
        return reply;
      };
      dispatcher_.addCommand(Msg.Command.sendFile, action4);
    }
    /*----< bind client processing to message types >--------------*/
    /*
     *  This is where we determine how incoming messages are
     *  processed by client.
     */
    void initializeDispatcher2()
    {
      // build result
      Func<Msg, Msg> action5 = (Msg msg) =>
      {
        btnShowLogs_Click(null, null);  // refresh request list view
        string display = msg.argument + " -- " + msg.to + " " + DateTime.Now.ToString();
        inMsgListBox.Items.Insert(0, display);
        if (inMsgListBox.Items.Count > maxInMsgCount)
          inMsgListBox.Items.RemoveAt(maxInMsgCount);
        Msg reply = new Msg(Msg.MessageType.noReply);
        return reply;
      };
      dispatcher_.addCommand(Msg.Command.buildResult, action5);

      // test result
      Func<Msg, Msg> action6 = (Msg msg) =>
      {
        btnShowLogs_Click(null, null);  // refresh log list view
        string display = msg.argument + " -- " + msg.to + " " + DateTime.Now.ToString();
        inMsgListBox.Items.Insert(0, display);
        if (inMsgListBox.Items.Count > maxInMsgCount)
          inMsgListBox.Items.RemoveAt(maxInMsgCount);
        Msg reply = new Msg(Msg.MessageType.noReply);
        return reply;
      };
      dispatcher_.addCommand(Msg.Command.testResult, action6);
    }
    /*----< display code in text area of popup >------*/

    private void showFile(string fileName, Window1 popUp)
    {
      string path = System.IO.Path.Combine(ClientEnvironment.storagePath, fileName);
      Paragraph paragraph = new Paragraph();
      string fileText = "";
      try
      {
        fileText = System.IO.File.ReadAllText(path);
      }
      finally
      {
        paragraph.Inlines.Add(new Run(fileText));
      }

      // add code text to code view
      popUp.codeView.Blocks.Clear();
      popUp.codeView.Blocks.Add(paragraph);
    }
    /*----< make CommMessage using GUI info >----------------------*/

    CommMessage makeMessage(CommMessage.MessageType msgType)
    {
      CommMessage msg = new CommMessage(msgType);
      msg.to = "http://localhost:8080/IPluggableComm";
      msg.from = "http://localhost:8081/IPluggableComm";
      msg.author = "Kaiqi";
      return msg;
    }
    /*----< create comm if needed >--------------------------------*/
    /*
     *  - communication may start in several different ways
     *  - we do a lazy initialization of comm_, so this code
     *    will be invoked when needed in a few different code
     *    locations
     */
    void createCommIfNeeded()
    {
      try
      {
        if (comm_ == null)
        {
          string localMachine = "http://localhost";
          int localPort = 8081;
          comm_ = new Comm(localMachine, localPort);
        }
      }
      catch (Exception ex)
      {
        Console.Write("\n-- {0}", ex.Message);
        GC.SuppressFinalize(this);
        System.Diagnostics.Process.GetCurrentProcess().Close();
      }
    }
    /*----< filter messages to process >---------------------------*/
    /*
     *  currently doesn't filter anything
     */
    bool doProcess(Msg msg)
    {
      //if (msg.type == CommMessage.MessageType.connect)
      //  return false;
      //if (msg.from == makeLocalEndpoint())
      //  return false;

      return true;  // currently doesn't filter anything
    }
    /*----< receive thread processing >----------------------------*/
    /*
     *  - received messages are processed by dispatcher
     *  - dispatcher has a dictionary of actions, based on the message type
     *  - this allows a lot of flexibility configuring application processing,
     *    e.g., can simply add or replace dispatcher actions
     */
    void rcvProc(string localMachine, string localPort)
    {
      createCommIfNeeded();
      //string localEndpoint = "http://localhost:8081/IPluggableComm";
      while (true)
      {
        CommMessage msg = comm_.getMessage();
        //Console.Write("\n  Received {0} message : {1}", msg.type.ToString(), msg.command.ToString());
        if (doProcess(msg))
        {
          Action toMainThrd = () =>
          {
            //displayInComingMsg(msg);
            Msg result = dispatcher_.doCommand(msg.command, msg);  // our Comm dispatcher
          };
          Dispatcher.BeginInvoke(toMainThrd);  // WPF's dispatcher lets child thread use window
        }
      }
    }
    /*----< make msg list display string >-------------------------*/

    string makeMsgDisplayStr(Msg msg)
    {
      string display = msg.type.ToString() + ":" + msg.command + " -- " + msg.to + " " + DateTime.Now.ToString();
      return display;
    }
    /*----< display incoming message >-----------------------------*/

    public void displayInComingMsg(Msg msg)
    {
      inMsgListBox.Items.Insert(0, makeMsgDisplayStr(msg));
      if (inMsgListBox.Items.Count > maxInMsgCount)
        inMsgListBox.Items.RemoveAt(maxInMsgCount);
    }
    /*----< start process pool with specified process number>-----*/

    private void btnStart_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        createCommIfNeeded();
        statusLabel.Text = "Status: start " + tbxProcessNum.Text + " process pool";
        CommMessage msg = makeMessage(CommMessage.MessageType.request);
        msg.command = Msg.Command.startProcessPool;
        msg.argument = tbxProcessNum.Text;
        comm_.postMessage(msg);
      }
      catch
      {
        statusLabel.Text = "Error: can't connect";
      }
    }
    /*----< shutdown all child builder process >------------------*/

    private void btnShutdown_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        createCommIfNeeded();
        statusLabel.Text = "Status: shutdown process pool";
        CommMessage msg = makeMessage(CommMessage.MessageType.request);
        msg.command = Msg.Command.closeProcessPool;
        comm_.postMessage(msg);
      }
      catch
      {
        statusLabel.Text = "Error: can't connect";
      }
    }
    /*----< retrieve file list from repo server >----------------*/

    private void btnShowFiles_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        createCommIfNeeded();
        statusLabel.Text = "Status: show files on repo server";
        CommMessage msg = makeMessage(CommMessage.MessageType.request);
        msg.to = "http://localhost:8082/IPluggableComm";
        msg.command = Msg.Command.getFiles;
        msg.argument = "";
        comm_.postMessage(msg);
      }
      catch
      {
        statusLabel.Text = "Error: can't connect";
      }
    }
    /*----< retrieve build request list from repo server >--------*/

    private void btnShowRequests_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        createCommIfNeeded();
        statusLabel.Text = "Status: show build requests on repo server";
        CommMessage msg = makeMessage(CommMessage.MessageType.request);
        msg.to = "http://localhost:8082/IPluggableComm";
        msg.command = Msg.Command.getRequests;
        msg.argument = "";
        comm_.postMessage(msg);
      }
      catch
      {
        statusLabel.Text = "Error: can't connect";
      }
    }
    /*----< retrieve build and test log list from repo server >-----*/

    private void btnShowLogs_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        createCommIfNeeded();
        statusLabel.Text = "Status: show build and test logs on repo server";
        CommMessage msg = makeMessage(CommMessage.MessageType.request);
        msg.to = "http://localhost:8082/IPluggableComm";
        msg.command = Msg.Command.getLogs;
        msg.argument = "";
        comm_.postMessage(msg);
      }
      catch
      {
        statusLabel.Text = "Error: can't connect";
      }
    }
    /*----< clear code file ListBox selection >-------------------*/

    private void btnClearSelection_Click(object sender, RoutedEventArgs e)
    {
      fileListBox.UnselectAll();
    }
    /*----< add a new test to the build request structure >-------*/

    private void btnAddToRequest_Click(object sender, RoutedEventArgs e)
    {
      if (tr == null)
      {
        tr = new TestRequest();
        tr.author = "Kaiqi Zhang";
      }

      TestElement te = new TestElement();
      te.buildTool = "MSBuild";

      // build config xml
      string configFile = "";
      foreach (var item in fileListBox.SelectedItems)
      {
        string fileName = (string)item;
        if (System.IO.Path.GetExtension(fileName) == ".csproj")
          configFile = fileName;
      }
      if (configFile == "")
      {
        MessageBox.Show("Please select one BuildConfig file (*.csproj).", "Add to Request",
          MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }
      te.buildConfig = configFile;
      te.testName = System.IO.Path.GetFileNameWithoutExtension(configFile);

      // cs files
      foreach (var item in fileListBox.SelectedItems)
      {
        string fileName = (string)item;
        if (System.IO.Path.GetExtension(fileName) == ".cs")
          te.addCode(fileName);
      }

      tr.tests.Add(te);

      requestTextBox.Text = tr.ToXml();
    }
    /*----< reset build request content to default >----------------*/

    private void btnResetRequest_Click(object sender, RoutedEventArgs e)
    {
      tr = new TestRequest();
      tr.author = "Kaiqi Zhang";

      requestTextBox.Text = tr.ToXml();
    }
    /*----< generate test request from selected files and send >----*/

    private void btnSendRequest_Click(object sender, RoutedEventArgs e)
    {
      if (tr.tests.Count == 0)
      {
        MessageBox.Show("Please add at least one request.", "Send Request",
          MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }
      string trXml = tr.ToXml();

      try
      {
        createCommIfNeeded();
        statusLabel.Text = "Status: send test request to repo server";
        CommMessage msg = makeMessage(CommMessage.MessageType.request);
        msg.to = "http://localhost:8082/IPluggableComm";
        msg.command = Msg.Command.testRequest;
        msg.argument = trXml;
        comm_.postMessage(msg);
        // refresh request list after send
        btnShowRequests_Click(null, null);
      }
      catch
      {
        statusLabel.Text = "Error: can't connect";
      }
    }
    /*----< send  test request on repo with the selected file >----*/

    private void btnSendRequestOnRepo_Click(object sender, RoutedEventArgs e)
    {
      if (requestListBox.SelectedIndex != -1)
      {
        string fileName = (string)requestListBox.SelectedItem;

        try
        {
          createCommIfNeeded();
          statusLabel.Text = "Status: send request on repo server";
          CommMessage msg = makeMessage(CommMessage.MessageType.request);
          msg.to = "http://localhost:8082/IPluggableComm";
          msg.command = Msg.Command.testRequestOnRepo;
          msg.argument = fileName;
          comm_.postMessage(msg);
        }
        catch
        {
          statusLabel.Text = "Error: can't connect";
        }
      }
    }
    /*----< popup a window to show a build request on repo >------*/

    private void requestListBox_DoubleClick(object sender, RoutedEventArgs e)
    {
      string fileName = (string)requestListBox.SelectedItem;

      try
      {
        createCommIfNeeded();
        statusLabel.Text = "Status: get request file from repo server";
        CommMessage msg = makeMessage(CommMessage.MessageType.request);
        msg.to = "http://localhost:8082/IPluggableComm";
        msg.command = Msg.Command.sendFile;
        msg.arguments.Add(fileName);
        comm_.postMessage(msg);
      }
      catch
      {
        statusLabel.Text = "Error: can't connect";
      }
    }
    /*----< popup a window to show a build/test log on repo >----*/

    private void logListBox_DoubleClick(object sender, RoutedEventArgs e)
    {
      string fileName = (string)logListBox.SelectedItem;

      try
      {
        createCommIfNeeded();
        statusLabel.Text = "Status: get log file from repo server";
        CommMessage msg = makeMessage(CommMessage.MessageType.request);
        msg.to = "http://localhost:8082/IPluggableComm";
        msg.command = Msg.Command.sendFile;
        msg.arguments.Add(fileName);
        comm_.postMessage(msg);
      }
      catch
      {
        statusLabel.Text = "Error: can't connect";
      }
    }
    /*----< for test executive use >------------------------------*/

    public void testShowFiles()
    {
      Action toMainThrd = () =>
      {
        btnShowFiles_Click(null, null);
        btnShowRequests_Click(null, null);
        btnShowLogs_Click(null, null);
      };
      Dispatcher.BeginInvoke(toMainThrd);  // WPF's dispatcher lets child thread use window
    }
    /*----< for test executive use >------------------------------*/

    public void testStartPool()
    {
      Action toMainThrd = () =>
      {
        tbxProcessNum.Text = "2";
        btnStart_Click(null, null);
      };
      Dispatcher.BeginInvoke(toMainThrd);  // WPF's dispatcher lets child thread use window
    }
    /*----< make mock test request, for test executive use >------*/

    private TestRequest makeTestRequest(int i)
    {
      TestElement te = new TestElement();

      if (i == 0)
      {
        te.testName = "Test1";
        te.buildTool = "MSBuild";
        te.buildConfig = "Test1.csproj";
        te.addCode("Test1_Interfaces.cs");
        te.addCode("Test1_TestedLib.cs");
        te.addCode("Test1_TestedLibDependency.cs");
        te.addCode("Test1_TestLib.cs");
      }
      else if (i == 1)
      {
        te.testName = "Test2";
        te.buildTool = "MSBuild";
        te.buildConfig = "Test2.csproj";
        te.addCode("Test2_Interfaces.cs");
        te.addCode("Test2_TestedLib.cs");
        te.addCode("Test2_TestLib.cs");
      }

      TestRequest tr = new TestRequest();
      tr.author = "Kaiqi Zhang";
      tr.tests.Add(te);

      return tr;
    }
    /*----< for test executive use >------------------------------*/

    public void testSendRequest()
    {
      Action toMainThrd = () =>
      {
        for (int i = 0; i < 2; i++)
        {
          string trXml = makeTestRequest(i).ToXml();

          try
          {
            createCommIfNeeded();
            statusLabel.Text = "Status: send test request to repo server";
            CommMessage msg = makeMessage(CommMessage.MessageType.request);
            msg.to = "http://localhost:8082/IPluggableComm";
            msg.command = Msg.Command.testRequest;
            msg.argument = trXml;
            comm_.postMessage(msg);
          }
          catch
          {
            statusLabel.Text = "Error: can't connect";
          }
        }
        Thread.Sleep(800);
      };
      Dispatcher.BeginInvoke(toMainThrd);  // WPF's dispatcher lets child thread use window
    }
    /*----< for test executive use >------------------------------*/

    public void testSendRequestOnRepo()
    {
      Action toMainThrd = () =>
      {
        try
        {
          createCommIfNeeded();
          statusLabel.Text = "Status: send test request on repo server";
          CommMessage msg = makeMessage(CommMessage.MessageType.request);
          msg.to = "http://localhost:8082/IPluggableComm";
          msg.command = Msg.Command.testRequestOnRepo;
          msg.argument = "BuildRequest-Sample3-MultiTests.xml";
          comm_.postMessage(msg);
        }
        catch
        {
          statusLabel.Text = "Error: can't connect";
        }
      };
      Dispatcher.BeginInvoke(toMainThrd);  // WPF's dispatcher lets child thread use window
    }
  }
}

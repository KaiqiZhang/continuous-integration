/////////////////////////////////////////////////////////////////////
//  TestExecutive.cs - demonstrate project 3 requirements          //
//  ver 1.0                                                        //
//  Language:      Visual C++ 2017                                 //
//  Platform:      Microsoft Surface, Windows 10                   //
//  Application:   Used to perform code publisher                  //
//  Author:        Kaiqi Zhang, Syracuse University                //
//                 kzhang17@syr.edu                                //
/////////////////////////////////////////////////////////////////////
/*
Package Operations:
==================
This package defines class TestCodePub, that demonstrates each of
the requirements in project #3 met.

Public Interface:
=================
TestExecutive exec = new TestExecutive();  // create an instance
exec.DemoReq(window);                      // demonstrate all requirement

Build Process:
==============
Required files
- TestExecutive.cs
- MainWindow.xaml.cs

Maintenance History:
====================
ver 1.0 : 29 Oct 2017
- first release

*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using MessagePassingComm;

namespace RemoteBuildServer
{
  class TestExecutive
  {
    //----< Demonstrate all requirements >---------------------------------
    public void DemoReq(MainWindow wnd)
    {
      TestUtilities.title("Remote Build Server - Demonstration", '=');
      Console.Write("\n  Notice: Please make sure you're running as Administrator\n");
      TestUtilities.putLine();

      DemoReq1(wnd);
      DemoReq2(wnd);
      DemoReq3(wnd);
      DemoReq4(wnd);
      DemoReq5(wnd);
      DemoReq6(wnd);

      Console.Write("\n  All functions have been demostrated. You can play around the GUI now.\n");
    }
    //----< Demonstrate step #1 >-----------------------------------

    private void DemoReq1(MainWindow wnd)
    {
      TestUtilities.title("Step 1 - Get file from repo", '=');
      Console.Write("\n  Sending request to get code, xml and log files list on repo.");

      wnd.testShowFiles();

      Console.Write("\n  Code files, XML build requests and log files are retrived.\n");
    }
    //----< Demonstrate step #2 >-----------------------------------

    private void DemoReq2(MainWindow wnd)
    {
      TestUtilities.title("Step 2 - Start process poll", '=');
      Console.Write("\n  In my design, Client can send messages to Mother Builder using WCF, to open a specified");
      Console.Write("\n  number of child builders.\n");

      Console.Write("\n  Sending message to open 2 child builders...");
      wnd.testStartPool();
      Thread.Sleep(1000);
      Console.Write("\n\n  Please check whether 2 child builders have been started.\n");
    }
    //----< Demonstrate step #3 >-----------------------------------
    private void DemoReq3(MainWindow wnd)
    {
      TestUtilities.title("Step 3 - Create BuildRequest and send", '=');

      Console.Write("\n  Sending build request:");
      Console.Write("\n  Client => Repo Server => Mother Builder => Child Builder(if ready)");

      wnd.testSendRequest();

      Console.Write("\n  2 requests sent to demonstrate the queue feature.");
      Console.Write("\n  One request shall build success and one shall build failed.");
      Console.Write("\n  Please check whether Child Builder printed out the build request.\n");

      Console.Write("\n  The Child Builder shall request files from Repo Server then.");
      Console.Write("\n  You may check the files in Child Storage path.\n");

      Console.Write("\n  The Child Builder will build the request and send each test to TestHarness.");
      Console.Write("\n  The Child Builder will send a Ready Message to Mother when finish building.\n");
      Thread.Sleep(1000);
    }
    //----< Demonstrate step #4 >-------------------------------

    private void DemoReq4(MainWindow wnd)
    {
      TestUtilities.title("Step 4 - Send request stored on repo", '=');
      Console.Write("\n  Commanding the repo to send build request \"\"...");
      Console.Write("\n  XML file: BuildRequest-Sample3-MultiTests.xml");

      wnd.testSendRequestOnRepo();

      Console.Write("\n  Please check whether Child Builder is building the request.\n");
    }
    //----< Demonstrate step #5 >------------------------------

    private void DemoReq5(MainWindow wnd)
    {
      TestUtilities.title("Step 5 - Build request storage", '=');

      Console.Write("\n  Everytime a BuildRequest is send from client, the repo will store it with timestamp.");
      Console.Write("\n  Double click the xml files on the mid-left ListBox to view the stored BuildRequests\n");
    }
    //----< Demonstrate step #6 >------------------------------

    private void DemoReq6(MainWindow wnd)
    {
      TestUtilities.title("Step 6 - Check build/test results and logs", '=');

      Console.Write("\n  Check messages in the bottom ListBox for build and test results");
      Console.Write("\n  Double click the log files on the mid-right ListBox to view the logs\n");
    }
  }
}

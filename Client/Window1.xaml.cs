///////////////////////////////////////////////////////////////////////////
// Window1.xaml.cs - Client GUI for code federation                      //
// Ver 1.0                                                               //
// Kaiqi Zhang, Jim Fawcett                                              //
// CSE681 Software Modeling & Analysis, Fall 2017                        //
///////////////////////////////////////////////////////////////////////////
/*  
 *  Purpose:
 *    Prototype for a secondary popup window for the Client,
 *    used to display text of files
 *
 *  Required Files:
 *    MainWindow.xaml, MainWindow.xaml.cs
 *    Window1.xaml, Window1.xaml.cs
 *
 *  Maintenance History:
 *  --------------------
 *  ver 1.0 : 6 Dec 2017
 *  - first release
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
using System.Windows.Shapes;

namespace RemoteBuildServer
{
  /// <summary>
  /// Interaction logic for Window1.xaml
  /// </summary>
  public partial class Window1 : Window
  {
    public Window1()
    {
      InitializeComponent();
    }
  }
}

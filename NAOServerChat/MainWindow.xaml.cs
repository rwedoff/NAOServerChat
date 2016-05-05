using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.ComponentModel;

namespace NAOServerChat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly BackgroundWorker worker;
        
        public MainWindow()
        {
            InitializeComponent();
            worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.WorkerSupportsCancellation = true;
        }


        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Console.WriteLine("Here");
            object[] parameters = e.Argument as object[];   
            AsynchronousSocketListener.StartListening((IPAddress) parameters[0], (int)parameters[1]);
            e.Result = "Server started...";
        }

       
        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            // run all background tasks here
            String port = portBox.Text;
            int tempPort = 0;
            try
            {
                int numPort = Int32.Parse(port);
                tempPort = numPort;
            }
            catch
            {
                serverConsole.Text = "Port Error";
            }
            
            IPAddress ipAddr;
            try
            {
                if (tempPort != 0)
                {
                    ipAddr = IPAddress.Parse(otherBox.Text);
                    object[] parameters = new object[] { ipAddr, tempPort };
                    worker.RunWorkerAsync(parameters);
                    serverConsole.Text = "Server is running...";
                    cancelButton.Visibility = Visibility.Visible;
                    ipBox.IsEnabled = false;
                    portBox.IsEnabled = false;
                    otherBox.IsEnabled = false;
                }
            }
            catch
            {
                serverConsole.Text = "IP Address Error";
            }
            
        }

        private void ipBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;
            Object selectedIP = box.SelectedItem;
            otherBox.Text = selectedIP.ToString();
        }

        private void ipBox_Loaded(object sender, RoutedEventArgs e)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());           
            ComboBox box = sender as ComboBox;
            foreach(IPAddress ip in ipHostInfo.AddressList)
            {
                box.Items.Add(ip);
            }
            box.Items.Insert(0,"Other");
        }


        private void otherBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ItemCollection boxItems = ipBox.Items;
            TextBox tb = sender as TextBox;
            if (!boxItems.Contains(tb.Text))
            {
                ipBox.SelectedIndex = 0;
            }
        }

        private void cancelButton_Click_1(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }
    }

    // State object for reading client data asynchronously
    public class StateObject
    {
        // Client  socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 1024;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }

    public class AsynchronousSocketListener
    {
        // Thread signal.
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        public static ArrayList socketList;
        public static bool _isRunning = true;
        public static string message = "";
        public AsynchronousSocketListener()
        {
        }

        public static void StartListening(IPAddress ip, int port)
        {
            // Data buffer for incoming data.
            byte[] bytes = new Byte[1024];
           
            IPEndPoint localEndPoint = new IPEndPoint(ip, port);

            // Create a TCP/IP socket.
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            socketList = new ArrayList();
            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (_isRunning)
                {
                    Console.WriteLine(_isRunning);
                    // Set the event to nonsignaled state.
                    allDone.Reset();
                    // Start an asynchronous socket to listen for connections.
                    Console.WriteLine("Waiting for a connection...");
                    message = "Waiting for a conection...";
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);
                    // Wait until a connection is made before continuing.
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                message = e.ToString();
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();

        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            allDone.Set();

            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
            socketList.Add(state);

        }

        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;
           
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;
            //Console.WriteLine(handler);

            // Read data from the client socket. 
            int bytesRead = 0;
            try
            {
                if(handler.IsBound)
                    bytesRead = handler.EndReceive(ar);
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e);
            }


            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.
                state.sb.Append(Encoding.ASCII.GetString(
                    state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read 
                // more data.
                content = state.sb.ToString();
                if (content.IndexOf("\n") > -1)
                {
                    // All the data has been read from the 
                    // client. Display it on the console.
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                        content.Length, content);
                    // Echo the data back to the client.
                    SendAll(handler, content);

                }
                else
                {
                    // Not all data received. Get more.
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }

                //TODO: Don't sent message to the client that sent it!
            }
        }

        private static void SendAll(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            foreach (StateObject state in socketList)
            {
                Socket socketHandler = state.workSocket;
                if (SocketConnected(socketHandler))
                {
                    // Begin sending the data to the remote device.
                    try
                    {
                        socketHandler.BeginSend(byteData, 0, byteData.Length, 0,
                        new AsyncCallback(SendCallback), socketHandler);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERROR: " + e);
                        message = e.ToString();
                    }
                }
                else
                {
                    if (!SocketConnected(socketHandler))
                    {
                        socketList.Remove(socketHandler);
                        socketHandler.Shutdown(SocketShutdown.Both);
                        socketHandler.Close();
                    }

                }

            }

        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.

                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);
                Console.WriteLine("Done");
                StateObject state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                //handler.Shutdown(SocketShutdown.Both);
                //handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static bool SocketConnected(Socket s)
        {
            bool part1 = false;
            bool part2 = false;
            try
            {
                part1 = s.Poll(1000, SelectMode.SelectRead);
                part2 = (s.Available == 0);
            }
            catch (ObjectDisposedException e)
            {
                Console.WriteLine(e);  // now I know object has been disposed
            }

            if (part1 && part2)
                return false;
            else
                return true;
        }
    }
}

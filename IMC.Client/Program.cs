using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EasyModbus;

// State object for receiving data from remote device.  
public class StateObject
{
    // Client socket.  
    public Socket workSocket = null;
    // Size of receive buffer.  
    public const int BufferSize = 256;
    // Receive buffer.  
    public byte[] buffer = new byte[BufferSize];
    // Received data string.  
    public StringBuilder sb = new StringBuilder();
}

public class AsynchronousClient
{
    // The port number for the remote device.  
    private const int port = 15800;

    // ManualResetEvent instances signal completion.  
    private static ManualResetEvent connectDone = new ManualResetEvent(false);
    private static ManualResetEvent sendDone = new ManualResetEvent(false);
    private static ManualResetEvent receiveDone = new ManualResetEvent(false);

    // The response from the remote device.  
    private static String response = String.Empty;

    private static string alarmTrigger = "611141414011|100013|02|";
    private static string resetalarm = "612144830013|100013|01|02|";
    private static string sentStatus = "619144955013|100013|01|02|";


    private static void StartClient()
    {
        // Connect to a remote device.  
        try
        {
            // Establish the remote endpoint for the socket.  
            // The name of the   
            // remote device is "host.contoso.com".  
            //IPHostEntry ipHostInfo = Dns.GetHostEntry("host.contoso.com");
            //IPAddress ipAddress = ipHostInfo.AddressList[0];
            //IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("10.10.10.22"), port);

            //Modbus connections
            //ModbusClient modbusClient = new ModbusClient("10.10.10.181", 502);    //Ip-Address and Port of Modbus-TCP-Server


            while (true)
            {
                //reset connection everytime loop 
                connectDone.Reset();
                sendDone.Reset();
                receiveDone.Reset();

                // Create a TCP/IP socket.  
                Socket client = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);
                // Connect to the remote endpoint.  
                client.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();

  
                //client choose message to send 
                Console.WriteLine("\nChoose message to send server ?\n");
                Console.WriteLine("[1] Alarm Trigger", alarmTrigger);
                Console.WriteLine("[2] Reset Alarm", resetalarm);
                Console.WriteLine("[3] Sent Status", sentStatus);
                Console.WriteLine("[0] Exit");
                string userinput = Console.ReadLine();

                //change user input to random message send 
                //Random random = new Random();
                //int randomNum = random.Next(1, 4);
                //Console.WriteLine(randomNum);
                //Console.WriteLine("Your Select Option: {0}", randomNum);
                string messageFromUser = Message(userinput);
                //Thread.Sleep(5000);

                // Send test data to the remote device.  SEND MESSAGE TO SERVER!!!
                Send(client, messageFromUser);
                sendDone.WaitOne();

                // Receive the response from the remote device.  
                Receive(client);
                receiveDone.WaitOne();

                // Write the response to the console.  
                Console.WriteLine("Response received : {0}\n", response);

                //modbus response
                //if (response.Contains("621"))
                //{
                //    modbusClient.WriteSingleCoil(0001, true);
                //    modbusClient.Disconnect();
                //}
                //else if (response.Contains("622"))
                //{
                //    modbusClient.WriteSingleCoil(0001, false);
                //    modbusClient.Disconnect();
                //}
                //else
                //{
                //    //
                //}


                // Release the socket.  
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    //message to send  to server 
    private static string Message(string input)
    {
        string FinalMsg = "";

        switch (input)
        {
            case "1":

                FinalMsg = alarmTrigger;
                Console.WriteLine("Alarm Trigger Message was send.");
                break;
            case "2":
                FinalMsg = resetalarm;
                Console.WriteLine("Reset Alarm Message was send.");
                break;
            case "3":
                FinalMsg = sentStatus;
                Console.WriteLine("Sent Status Message was send.");
                break;
            default:
                Console.WriteLine("Invalid. Please enter 1, 2 or 3. To Exit press 0.");
                Environment.Exit(0);
                break;
        }
        return FinalMsg;
    }

    private static void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket client = (Socket)ar.AsyncState;

            // Complete the connection.  
            client.EndConnect(ar);

            Console.WriteLine("Socket connected to {0}",
                client.RemoteEndPoint.ToString());

            // Signal that the connection has been made.  
            connectDone.Set();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    private static void Receive(Socket client)
    {
        try
        {
            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = client;

            // Begin receiving the data from the remote device.  
            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReceiveCallback), state);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    private static void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the state object and the client socket   
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket client = state.workSocket;

            // Read data from the remote device.  
            int bytesRead = client.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There might be more data, so store the data received so far.  
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                // Get the rest of the data.  
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            else
            {
                // All the data has arrived; put it in response.  
                if (state.sb.Length > 1)
                {
                    response = state.sb.ToString();
                }
                // Signal that all bytes have been received.  
                receiveDone.Set();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    private static void Send(Socket client, String data)
    {
        // Convert the string data to byte data using ASCII encoding.  
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.  
        client.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), client);
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket client = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSent = client.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to server.", bytesSent);

            // Signal that all bytes have been sent.  
            sendDone.Set();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    public static int Main(String[] args)
    {
        StartClient();
        Console.ReadKey();
        return 0;
    }
}

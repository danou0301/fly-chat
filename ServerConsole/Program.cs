using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections;

namespace ServerConsole
{

    class Program
    {
        public static ArrayList clients;
        private const int PORT = 8080;
        private const string IP_LOCAL = "127.0.0.1";
        private static string SAVE_FILES_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\";

        private static void saveFile(TcpClient inClientSocket, string file_name)
        {
            NetworkStream fileStream = inClientSocket.GetStream();
            // Read length of incoming data
            byte[] length = new byte[4];
            int bufferSize = 1024;
            int bytesRead = 0;
            int allBytesRead = 0;

            bytesRead = fileStream.Read(length, 0, 4);
            int dataLength = BitConverter.ToInt32(length, 0);
            
            // Read the data
            int bytesLeft = dataLength;
            byte[] data = new byte[dataLength];

            while (bytesLeft > 0)
            {
                //Console.WriteLine("bytes download: " + Convert.ToString(bytesLeft));
                int nextPacketSize = (bytesLeft > bufferSize) ? bufferSize : bytesLeft;

                bytesRead = fileStream.Read(data, allBytesRead, nextPacketSize);
                allBytesRead += bytesRead;
                bytesLeft -= bytesRead;

            }
            // Save file on server
            try
            {
                string path = SAVE_FILES_PATH + file_name;
                File.WriteAllBytes(path, data);
                Console.WriteLine("Finish !" + " Save in " + SAVE_FILES_PATH + file_name);

            }
            catch (Exception ex)
            {
                Console.WriteLine("error save file: " + ex);
            }

        }
        private static void sendFile(TcpClient inClientSocket, string file_name)
        {
            // send file to the server
            NetworkStream fileStream = inClientSocket.GetStream();
            byte[] fileNameByte = Encoding.ASCII.GetBytes("@-@: " + file_name); // to do: not safe if in the chat write @-@
            fileStream.Write(fileNameByte, 0, fileNameByte.Length);
            try
            {
                byte[] data = File.ReadAllBytes(SAVE_FILES_PATH + file_name);
                // Build the package
                byte[] dataLength = BitConverter.GetBytes(data.Length);
                //Console.WriteLine("file size: ", data.Length);
                byte[] package = new byte[4 + data.Length];
                dataLength.CopyTo(package, 0);
                data.CopyTo(package, 4);

                // Send to server
                int bufferSize = 1024;
                int bytesSent = 0;
                int bytesLeft = package.Length;
                while (bytesLeft > 0)
                {

                    int nextPacketSize = (bytesLeft > bufferSize) ? bufferSize : bytesLeft;
                    fileStream.Write(package, bytesSent, nextPacketSize);
                    bytesSent += nextPacketSize;
                    bytesLeft -= nextPacketSize;

                }
                Console.WriteLine("File send !");
            } catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex);
            }
            
        }

        private static void doChat(TcpClient inClientSocket, string clineNo)
        {
            string dataFromClient = null;
            NetworkStream networkStream;

            while (true)
            {
                try
                {
                    byte[] bytesFrom = new byte[10025];
                    networkStream = inClientSocket.GetStream();
                    int bytesRead = networkStream.Read(bytesFrom, 0, 10000); // get message from user
                    dataFromClient = Encoding.ASCII.GetString(bytesFrom, 0, bytesRead);
                    Console.WriteLine(" >> " + "From client-" + clineNo + "  "+ dataFromClient);
                    if (dataFromClient.IndexOf("$-$: ") == 0) // save file
                    {
                        string file_name = dataFromClient.Substring(5);
                        Console.WriteLine("Start download file: " + file_name + Convert.ToString(dataFromClient.Length));
                        saveFile(inClientSocket, file_name);

                    } else if (dataFromClient.IndexOf("@-@: ") == 0) // send file
                    {
                        string file_name = dataFromClient.Substring(5);
                        Console.WriteLine("Start Send file...");
                        sendFile(inClientSocket, file_name);
                    }

                    // send the message to all users in the server
                    foreach (TcpClient cl in clients)
                    {
                        byte[] toSend = Encoding.ASCII.GetBytes(dataFromClient);

                        if ((cl != inClientSocket || dataFromClient.IndexOf("$-$: ") == 0) && dataFromClient.IndexOf("@-@: ") == -1)
                        {
                            NetworkStream clStream = cl.GetStream();
                            clStream.Write(toSend, 0, toSend.Length);
                            clStream.Flush();
                        }
                    }
                }
                catch (Exception ex) // the user is disconnected
                {
                    Console.WriteLine(" >> exit user:" + clineNo);
                    clients.Remove(inClientSocket);
                    Console.WriteLine("Num user connected: " + Convert.ToString(clients.Count));
                    inClientSocket.Close();
                    return;
                }
            }
        }


        static void Main(string[] args)
        {
            IPAddress ipAddr = IPAddress.Parse(IP_LOCAL);
            TcpListener serverSocket = new TcpListener(ipAddr, PORT);
            TcpClient clientSocket = default(TcpClient);
            int counter = 0;

            clients = new ArrayList();
            serverSocket.Start();
            Console.WriteLine(" >> " + "Server Started");

            try
            {
                while (true)
                {

                    clientSocket = serverSocket.AcceptTcpClient(); // accept the client
                    clients.Add(clientSocket); // add the client to the list of clients
                    // start chat
                    Thread ctThread = new Thread(() =>
                    {
                        counter += 1;
                        Console.WriteLine("Num user connected: " + Convert.ToString(clients.Count));
                        Console.WriteLine(" >> " + "Client No:" + Convert.ToString(counter) + " started!");
                        doChat(clientSocket, Convert.ToString(counter));
                    }
                    );
                    
                    ctThread.Start();
                }
            }
            catch(Exception ex)
            {
                serverSocket.Stop();
                return;
            }
            

        }
    }


}

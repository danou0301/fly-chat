using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace ChatBox
{
    public partial class Form1 : Form
    {

        private const int PORT = 80;
        private const string IP_LOCAL = "127.0.0.1";
        private const string SAVE_FILES_PATH = "C:\\Users\\dan\\Desktop\\user\\";
        private bool isDownloadFile = false;

        TcpClient serverSocket;
        NetworkStream serverStream;

        public Form1()
        {
            InitializeComponent();
        }

        private void send_msg_Click(object sender, EventArgs e)
        {
            if (textMsg.Text == "" || serverStream == null) // if send empty message not send to the server
            {
                return;
            }
            try
            {
                byte[] outStream = Encoding.ASCII.GetBytes(textMsg.Text);
                serverStream.Write(outStream, 0, outStream.Length);
                serverStream.Flush();
                listMsg.Items.Add("- " + textMsg.Text);

                textMsg.Text = "";
            }
            catch (Exception ex)
            {
                Console.WriteLine("error connection interupt " + ex);
                return;
            }


        }


        private void Form1_Load(object sender, EventArgs e)
        {
            serverSocket = new TcpClient();
            
            try
            {
                serverSocket.Connect(IP_LOCAL, PORT); // connect to server
                serverStream = serverSocket.GetStream();
                backgroundWorker3.RunWorkerAsync();
                backgroundWorker1.WorkerSupportsCancellation = true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("failed to connect to server " + ex);
                return;
            }

        }


        private void backgroundWorker3_DoWork(object sender, DoWorkEventArgs e)
        {
            while (serverSocket.Connected)
            {
                try
                {
                    if (!this.isDownloadFile) { // todo: if the user download file he doesn't listent the server, have to do on a thread
                        byte[] inStream = new byte[10025];
                        int bytesRead = serverStream.Read(inStream, 0, 10000);
                        string getdata = Encoding.ASCII.GetString(inStream, 0, bytesRead);
                        
                        if (getdata.IndexOf("$-$: ") == 0) // get file name
                        {
                            string fileName = getdata.Substring(5);
                            listFiles.Items.Add("> " + fileName);
                        }
                        else if (getdata.IndexOf("@-@: ") != 0) // get message, check that isn't a file message
                        {
                            listMsg.Items.Add("> " + getdata);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error get from the server: " + ex);
                    return;
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "files (*.xml;*.json)|*.xml;*.json"; // show just compatible files
            ofd.ShowDialog();  // open file dialog 

            string filePath = ofd.FileName;
            string safeFilePath = ofd.SafeFileName;  // filename without path
            string file_extention = safeFilePath.Split('.').Last();
            // check extention
            if (file_extention != "json" && file_extention != "xml")
            {
                Console.WriteLine("Not Good Extention : " + file_extention);
                return;
            }

            // send file to the server
            byte[] fileNameByte = Encoding.ASCII.GetBytes("$-$: " + safeFilePath); // to do: not safe if in the chat write $
            serverStream.Write(fileNameByte, 0, fileNameByte.Length);

            byte[] data = File.ReadAllBytes(filePath);
            // Build the package
            byte[] dataLength = BitConverter.GetBytes(data.Length);
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
                serverStream.Write(package, bytesSent, nextPacketSize);
                bytesSent += nextPacketSize;
                bytesLeft -= nextPacketSize;
            }
        }


        private  void saveFile(string file_name)
        {
            NetworkStream fileStream;
            fileStream = serverSocket.GetStream();
            // Read length of incoming data
            byte[] length = new byte[4];
            int bufferSize = 1024;
            int allBytesRead = 0;

            int bytesRead = fileStream.Read(length, 0, 4);
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
            // Save file to the server
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


        private void listFiles_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int index = this.listFiles.IndexFromPoint(e.Location);
            if (index != System.Windows.Forms.ListBox.NoMatches)
            {
                // download the file from the server
                string file_name = listFiles.SelectedItem.ToString().Substring(2);
                MessageBox.Show("Download: " + file_name);

                // ask the server to send the file
                byte[] outStream = Encoding.ASCII.GetBytes("@-@: " + file_name);
                serverStream.Write(outStream, 0, outStream.Length);

                // download the file
                this.isDownloadFile = true;
                saveFile(file_name);
                this.isDownloadFile = false;

                MessageBox.Show("Finish !");

            }
        }
    }
}

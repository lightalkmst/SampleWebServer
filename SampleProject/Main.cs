using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SampleProject {
    class Application {
        // New files can be added in one place
        private static readonly string[][] WebFiles = {
            new string[] { "Main.html", "text/html" },
            new string[] { "Script.js", "text/javascript" },
            new string[] { "Colors.json", "application/json" },
            new string[] { "Style.css", "text/css" }
        };

        private const int MaxThreads = 4;
        private const int ConnectionPollingInterval = 4;

        // Could just use Tuple, but creating an immutable FileData class is more OOP convention
        private static readonly Dictionary<string, FileData> WebFileMap = new Dictionary<string, FileData>();

        private int Port;
        private TcpListener Listener;
        private string RootFilePath;

        public static int Main(string[] argv) {
            // So I won't be looked at funny for having "static" all over the place
            return new Application().ApplicationMain(argv);
        }

        private int ApplicationMain(string[] argv) {
            // Ask for path to web files and continue retrying until a path with all files available is given
            // Mostly to confirm that whoever is testing this is using it properly rather than running it and failing silently
            // As an example, if the project is loaded into Visual Studio and ran through that, the file path would be "../../"
            for (; ; ) {
                Console.Write("Enter the path to the web files: ");
                RootFilePath = Console.ReadLine();
                // Automatically append the folder delimiter to the end as necessary
                if (!RootFilePath.Substring(RootFilePath.Length - 1).Equals("/") && !RootFilePath.Substring(RootFilePath.Length - 1).Equals(@"\")) {
                    RootFilePath += @"\";
                }

                // Try to load all the files
                // If any exceptions are thrown while reading the program, ask again for the file path
                // Otherwise, all files were read correctly and the program will continue
                try {
                    foreach (string[] file in WebFiles) {
                        WebFileMap[file[0]] = new FileData(File.ReadAllText(RootFilePath + file[0]), file[1]);
                    }
                    Console.WriteLine("The web files were found");
                    break;
                }
                catch (Exception) {
                    Console.WriteLine("The web files were not found");
                }
            }

            Console.WriteLine();
            // Ask for the port to host the site on
            // Again, to make sure that the program is being tested properly and isn't hardcoded to use a port that may be in use
            for (; ; ) {
                Console.Write("Enter the port to host on: ");

                // Try to parse input as int and try opening a listener on the given port
                // If any exceptions are thrown while parsing or opening the listener, ask again for the port number
                // Otherwise, listener was made correctly and the program will continue
                try {
                    Port = Convert.ToInt32(Console.ReadLine());
                    Listener = new TcpListener(Dns.GetHostEntry("localhost").AddressList[0], Port);
                    Console.WriteLine("The port was valid");
                    break;
                }
                catch (Exception) {
                    Console.WriteLine("The port was invalid");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Starting server");
            // Start the listener
            Listener.Start();
            // Start the root handler thread
            new Task(ConnectionHandler).Start();

            Console.WriteLine();
            Console.WriteLine("The server has been started on port {0}", Port);
            Console.WriteLine("The web application can be accessed at http://localhost:{0}", Port);
            Console.WriteLine("Press enter to end the server");
            Console.ReadLine();

            Listener.Stop();

            return 0;
        }

        private void ConnectionHandler() {
            int numThreads = 0;
            for (; ; ) {
                Socket sock = Listener.AcceptSocket();
                // Sleep until a thread is freed up if at max capacity
                for (; numThreads == MaxThreads; Thread.Sleep(ConnectionPollingInterval)) ;
                // Spawn a thread to handle the connection so that clients can be handled in parallel
                numThreads++;
                new Task(delegate() {
                    try {
                        // Using block to easily ensure that the resource is closed even on a thrown exception
                        // Since this is running in a separate thread from the main handler, exceptions do not affect it
                        //     so no extra try blocks are necessary
                        using (sock) {
                            // Check if the connection is closed to reduce unnecessary processing
                            if (!sock.Connected)
                                return;

                            byte[] byteBuffer = new byte[2048];
                            sock.Receive(byteBuffer, byteBuffer.Length, 0);

                            string strBuffer = Encoding.ASCII.GetString(byteBuffer);

                            string httpVersion = strBuffer.Substring(strBuffer.IndexOf("HTTP"), 8);

                            // Find the requested file
                            strBuffer = strBuffer.Replace("\\", "/");
                            string fileName = strBuffer.Substring(0, strBuffer.IndexOf(" HTTP"));
                            fileName = fileName.Substring(fileName.LastIndexOf("/") + 1);
                            // HTML args processing can be added here if the program were to be augmented
                            // Would need to parse a little more to split fileName to exclude the args
                            // Chained if-else statements to allow specific protocol handling and sends the error page if the page does not exist
                            // As it is now, I load each page and cache it in memory
                            // If we had an emphasis on memory over speed, we could dynamically load and serve the pages here without caching

                            // Example of special case addition
                            if (fileName.Equals("")) {
                                SendMessage(httpVersion, WebFileMap["Main.html"].Mime, WebFileMap["Main.html"].Body, sock);
                            }
                            // Serve all other pages that are available
                            else if (WebFileMap.ContainsKey(fileName)) {
                                SendMessage(httpVersion, WebFileMap[fileName].Mime, WebFileMap[fileName].Body, sock);
                            }
                            // If the page does not exist, send a 404 page
                            else {
                                SendMessage(httpVersion, "text/html", "<h1>404 File Not Found<h1>", sock, "404 Not Found");
                            }
                        }
                    }
                    finally {
                        // Reduce current thread count no matter how the thread exits
                        numThreads--;
                    }
                }).Start();
            }
        }

        private void SendMessage(string httpVersion, string messageType, string message, Socket sock, string messageStatus = "200 OK") {
            // Build the HTTP header and append the message
            String strBuffer = "";
            strBuffer += httpVersion + " " + messageStatus + "\r\n";
            strBuffer += "Server: cx1193719-b\r\n";
            strBuffer += "Content-Type: " + messageType + "\r\n";
            strBuffer += "Accept-Ranges: bytes\r\n";
            strBuffer += "Content-Length: " + message.Length + "\r\n\r\n";
            strBuffer += message;

            byte[] byteBuffer = Encoding.ASCII.GetBytes(strBuffer); 
            
            // Try sending the page up to numTries times
            const int numTries = 4;
            if (sock.Connected) {
                int status = -1;
                for (int i = 0; status == -1 && i < numTries; i++) {
                    try {
                        status = sock.Send(byteBuffer, byteBuffer.Length, 0);
                    }
                    catch (Exception) {
                    }
                }
            }
        }     

        private class FileData {
            // I believe that this is the convention for these
            public string Body { get; private set; }
            public string Mime { get; private set; }

            public FileData(string name, string mime) {
                this.Body = name;
                this.Mime = mime;
            }
        }
    }
}
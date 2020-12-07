/*=====================================================================
  File:      OPCCSharp.cs

  Summary:   OPC sample client for C#

-----------------------------------------------------------------------
  This file is part of the Viscom OPC Code Samples.

  Copyright(c) 2001 Viscom (www.viscomvisual.com) All rights reserved.

THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
PARTICULAR PURPOSE.
======================================================================*/

using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Configuration;
using OPC.Common;
using OPC.Data;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace CSSample
{
    class Tester
    {
        // ***********************************************************	EDIT THIS :
        string serverProgID = ConfigurationManager.AppSettings["opcID"];         // ProgID of OPC server
        private static int timeref = Int32.Parse(ConfigurationManager.AppSettings["refreshTime"]); // OPC refresh time
        private static string consoleOut = ConfigurationManager.AppSettings["consoleOutput"];

        static string[] rowsArray = File.ReadAllLines("tags.txt");
        static string[] tags = new string[rowsArray.Length];
        static string[] ratios = new string[rowsArray.Length];
        static string[] offsets = new string[rowsArray.Length];
        static string[] isbool = new string[rowsArray.Length];
        //web server settings
        private static string webSend = ConfigurationManager.AppSettings["webServer"];
        private static string portNumb = ConfigurationManager.AppSettings["portNumber"];
        // udp settings
        private static int sendtags = Int32.Parse(ConfigurationManager.AppSettings["tags2send"]);
        private static IPAddress remoteIPAddress = IPAddress.Parse(ConfigurationManager.AppSettings["remoteIP"]);
        private static int remotePort = Convert.ToInt16(ConfigurationManager.AppSettings["remotePort"]);
        private static string udpSend = ConfigurationManager.AppSettings["udpSend"];

        private OpcServer theSrv;
        private OpcGroup theGrp;
        private static float[] currentValues;
        private static string responseStringG = "";
        private static HttpListener listener = new HttpListener();
        
        public void Work()
        {
            /*	try						// disabled for debugging
                {	*/

            theSrv = new OpcServer();
            theSrv.Connect(serverProgID);
            Thread.Sleep(500);              // we are faster then some servers!

            // add our only working group
            theGrp = theSrv.AddGroup("OPCCSharp-Group", false, timeref);

           
            if (sendtags > tags.Length) sendtags = tags.Length;

            var itemDefs = new OPCItemDef[tags.Length];
            for (var i = 0; i < tags.Length; i++)
            {
                itemDefs[i] = new OPCItemDef(tags[i], true, i, VarEnum.VT_EMPTY);
            }

            OPCItemResult[] rItm;
            theGrp.AddItems(itemDefs, out rItm);
            if (rItm == null)
                return;
            if (HRESULTS.Failed(rItm[0].Error) || HRESULTS.Failed(rItm[1].Error))
            {
                Console.WriteLine("OPC Tester: AddItems - some failed"); theGrp.Remove(true); theSrv.Disconnect(); return;

            };

            var handlesSrv = new int[itemDefs.Length];
            for (var i = 0; i < itemDefs.Length; i++)
            {
                handlesSrv[i] = rItm[i].HandleServer;
            }

            currentValues = new Single[itemDefs.Length];

            // asynch read our two items
            theGrp.SetEnable(true);
            theGrp.Active = true;
            theGrp.DataChanged += new DataChangeEventHandler(this.theGrp_DataChange);
            theGrp.ReadCompleted += new ReadCompleteEventHandler(this.theGrp_ReadComplete);

            int CancelID;

            int[] aE;
            theGrp.Read(handlesSrv, 55667788, out CancelID, out aE);

            // some delay for asynch read-complete callback (simplification)
            Thread.Sleep(500);

            while (webSend == "yes")
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                context.Response.AddHeader("Access-Control-Allow-Origin", "*");


                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseStringG);
                // Get a response stream and write the response to it.
                response.ContentLength64 = buffer.Length;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                // You must close the output stream.
                output.Close();
            }
            // disconnect and close
            Console.WriteLine("************************************** hit <return> to close...");
            Console.ReadLine();
            theGrp.ReadCompleted -= new ReadCompleteEventHandler(this.theGrp_ReadComplete);
            theGrp.RemoveItems(handlesSrv, out aE);
            theGrp.Remove(false);
            theSrv.Disconnect();
            theGrp = null;
            theSrv = null;


            /*	}
            catch( Exception e )
                {
                Console.WriteLine( "EXCEPTION : OPC Tester " + e.ToString() );
                return;
                }	*/
        }

        // ------------------------------ events -----------------------------

        public void theGrp_DataChange(object sender, DataChangeEventArgs e)
        {

            foreach (OPCItemState s in e.sts)
            {
                if (HRESULTS.Succeeded(s.Error))
                {
                    if (consoleOut == "yes")
                    {
                        Console.WriteLine(" ih={0} v={1} q={2} t={3}", s.HandleClient, s.DataValue, s.Quality, s.TimeStamp);
                    }
                    try
                    {
                        currentValues[s.HandleClient] = Convert.ToSingle(s.DataValue) * Single.Parse(ratios[s.HandleClient]) + Single.Parse(offsets[s.HandleClient]);
                    }
                    catch (FormatException fex) {
                        Console.WriteLine("Неверный формат числа. Используй запятую вместо точки. {0} ", fex);
                        File.WriteAllText("error"+ DateTime.Now.ToString("HHmmss") + ".txt", "Неверный формат числа. Используй запятую вместо точки." + "\n " + fex.ToString() + "\n " + fex.Message);
                        theSrv.Disconnect();
                    }
                }
                else
                    Console.WriteLine(" ih={0}    ERROR=0x{1:x} !", s.HandleClient, s.Error);
            }
            string responseString = "{";
            for (int i = 0; i < currentValues.Length - 1; i++)
            {
                string value = "";
                if ((isbool[i] == "b" && currentValues[i] == 1) || (isbool[i] == "!b" && currentValues[i] == 0)) value = "true";
                else if ((isbool[i] == "b" && currentValues[i] == 0) || (isbool[i] == "!b" && currentValues[i] == 1)) value = "false";
                else value = currentValues[i].ToString();
                responseString = responseString + "\"tag" + i + "\":\"" + value + "\", ";

            }
            string valuelast = "";
            int lasti = currentValues.Length - 1;
            if ((isbool[lasti] == "b" && currentValues[lasti] == 1) || (isbool[lasti] == "!b" && currentValues[lasti] == 0)) valuelast = "true";
            else if ((isbool[lasti] == "b" && currentValues[lasti] == 0) || (isbool[lasti] == "!b" && currentValues[lasti] == 1)) valuelast = "false";
            else valuelast = currentValues[lasti].ToString();
            responseString = responseString + "\"tag" + (lasti) + "\":\"" + valuelast + "\"}";
            responseStringG = responseString;

            byte[] byteArray = new byte[sendtags * 4];
            Buffer.BlockCopy(currentValues, 0, byteArray, 0, byteArray.Length);

            if (udpSend == "yes") UDPsend(byteArray);
        }

        private static void UDPsend(byte[] datagram)
        {
            UdpClient sender = new UdpClient();
            IPEndPoint endPoint = new IPEndPoint(remoteIPAddress, remotePort);

            try
            {
                sender.Send(datagram, datagram.Length, endPoint);
                //Console.WriteLine("Sended", datagram);
            }
            catch (Exception ex)
            {
                Console.WriteLine("UDP exception: " + ex.ToString() + "\n  " + ex.Message);
            }
            finally
            {
                sender.Close();
            }
        }
        public void theGrp_ReadComplete(object sender, ReadCompleteEventArgs e)
        {
            //Console.WriteLine("ReadComplete event: gh={0} id={1} me={2} mq={3}", e.groupHandleClient, e.transactionID, e.masterError, e.masterQuality);
            foreach (OPCItemState s in e.sts)
            {
                if (HRESULTS.Succeeded(s.Error))
                {
                    //Console.WriteLine(" ih={0} v={1} q={2} t={3}", s.HandleClient, s.DataValue, s.Quality, s.TimeStamp);
                }
                else
                    Console.WriteLine(" ih={0}    ERROR=0x{1:x} !", s.HandleClient, s.Error);
            }
        }

        static void Main(string[] args)
        {
            // считываем вайл tags.txt и добавляем данные в массивы тегов, коэффициентов, оффсетов
            for (int i = 0; i < rowsArray.Length; i++)
            {
                string[] col = rowsArray[i].Split('\t');
                try
                {
                    tags[i] = col[1];
                    ratios[i] = col[2];
                    offsets[i] = col[3];
                    isbool[i] = col[4];
                    //Console.WriteLine(col[0] + " | " + col[1] + " | " + col[2] + " | " + col[3] + " | " + col[4]);
                }
                catch (IndexOutOfRangeException e)
                {
                    Console.WriteLine("Несоответствие столбцов, проверь что все столбцы заполнены и разделены табуляцией. {0} ", e);
                    File.WriteAllText("error" + DateTime.Now.ToString("HHmmss") + ".txt", "Несоответствие столбцов, проверь что все столбцы заполнены и разделены табуляцией." + "\n " + e.ToString() + "\n " + e.Message);
                }
                
            }
            string url = "http://*";
            string port = portNumb;
            string prefix = String.Format("{0}:{1}/", url, port);
            listener.Prefixes.Add(prefix);
            listener.Start();

            Tester tst = new Tester();
            tst.Work();
        }
    }
}
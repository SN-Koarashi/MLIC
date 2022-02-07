using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WMICC
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private string DNSLookup() {
            string ip = textBox1.Text;
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c nslookup -type=a " + ip + " 8.8.8.8",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            bool client = false;
            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                if (line.StartsWith("Address:"))
                {
                    if (client)
                        return line.Replace("Address: ", "");
                    else
                        client = true;
                }

                if (line.Contains("Non-existent domain"))
                {
                    return "404";
                }
            }

            return "404";
        }

        private List<string> WMICS() {
            List<string> result = new List<string>();
            string ip = DNSLookup();
            if (!ip.Equals("404"))
            {
                string cond = "commandline like '%-Djava.library.path%' and commandline like '%--gameDir%' and commandline like '%minecraft%'";
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c wmic process where \"caption = 'javaw.exe' and " + cond + " or caption = 'javaw.exe' and " + cond + "\" get processId /value",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                while (!proc.StandardOutput.EndOfStream)
                {
                    string line = proc.StandardOutput.ReadLine();
                    if (line.Length > 0)
                    {
                        string[] msg = line.Split(new string[] { "=" }, StringSplitOptions.None);
                        result.Add(msg[1]);
                    }
                }
            }
            else
            {
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show("Could not find IP Address", "Error", buttons);
            }

            return result;
        }

        private Array Netstat() {
            Array PID = WMICS().ToArray();
            string ip = DNSLookup();
            string port = textBox2.Text.Length > 0 ? textBox2.Text : "25565";
            string address = ip + ":" + port;

            List<string> result = new List<string>();

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c netstat -ano -p tcp",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                if (line.Length > 0)
                {
                    string msg = line.Trim();
                    if (msg.StartsWith("TCP") && msg.Contains("ESTABLISHED")) {
                        string clear = Regex.Replace(msg, @"\s+", " ");
                        var clearArr = clear.Split(' ');

                        if (clearArr[2].Contains(address.Trim()))
                        {
                            
                            foreach (string pid in PID)
                            {
                                
                                if (clearArr[4].Contains(pid))
                                {
                                    result.Add(clearArr[4]);
                                }
                            }
                        }
                    }
                }
            }

            return result.ToArray();
        }

        private Dictionary<string, string> WMIC_PID(string pid) {
            var dictionary = new Dictionary<string, string>();

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c wmic process where \"processId='"+pid+ "'\" get commandline /value",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                if (line.Length > 0)
                {
                    string msg = line.Replace("CommandLine=", "");
                    string[] msgArr = msg.Split(' ');
                    int index = 0;
                    foreach (string str in msgArr) {
                        if (str.Length > 0)
                        {
                            if (str.Contains("-Dminecraft.launcher.brand"))
                            {
                                dictionary.Add("launcher", str);
                            }
                            else if (str == ("--gameDir"))
                            {
                                dictionary.Add("gameDir", msgArr[index + 1]);
                            }
                            else if (str == ("--version"))
                            {
                                dictionary.Add("version", msgArr[index + 1]);
                            }
                            else if (str == ("--username"))
                            {
                                dictionary.Add("user", msgArr[index + 1]);
                            }
                            else if (index + 1 < msgArr.Length && index - 1 > 0)
                            {
                                if (msgArr[index + 1].StartsWith("--") && !msgArr[index - 1].StartsWith("--"))
                                {
                                    dictionary.Add("class", str);
                                }
                            }
                        }
                        index++;
                    }
                }
            }

            return dictionary;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Length <= 0) {
                MessageBox.Show("Please enter your connect IP address.", "Error", MessageBoxButtons.OK);
                textBox1.Focus();
                return;
            }
            if (textBox3.Text.Length <= 0)
            {
                MessageBox.Show("Please enter url that you want to send data to.", "Error", MessageBoxButtons.OK);
                textBox3.Focus();
                return;
            }


            button1.Enabled = false;

            var result = new Dictionary<string, string>();
            var arr = Netstat();

            if (arr.Length <= 0) {
                button1.Enabled = true;
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show($"You are not connecting to {textBox1.Text}", "Error", buttons);
                return;
            }

            foreach (string pid in arr)
            {
                var subResult = new Dictionary<string, string>();
                var dic = WMIC_PID(pid);
                var gamePath = dic["gameDir"]+"\\mods";

                subResult.Add("launcher", dic["launcher"]);
                subResult.Add("gameDir", dic["gameDir"]);
                subResult.Add("version", dic["version"]);
                subResult.Add("class", dic["class"]);
                subResult.Add("user", dic["user"]);

                if (Directory.Exists(gamePath)) {
                    string[] fileEntries = Directory.GetFiles(gamePath);
                    string filename = "";

                    foreach (string filePath in fileEntries) {
                        string[] fileArr = filePath.Split(new string[] { "\\" }, StringSplitOptions.None);
                        int ArrLen = fileArr.Length;
                        filename += fileArr[ArrLen - 1]+";";
                    }
                    subResult.Add("mods", (filename.Length > 0)? filename.Replace(";","\n"):"Not Found in Direct Read Stream");
                }

                string json = JsonConvert.SerializeObject(subResult);

                result.Add(pid,json);
            }
            var httpClient = new HttpClient();
            var ip = await httpClient.GetStringAsync("http://api.ipify.org");

            result.Add("IP", ip);

            string submit = JsonConvert.SerializeObject(result);
            byte[] byteArray = Encoding.UTF8.GetBytes(submit);

            Console.WriteLine(submit);

            WebRequest request = WebRequest.Create(textBox3.Text);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();

            WebResponse response = request.GetResponse();
            Console.WriteLine(((HttpWebResponse)response).StatusDescription);
            response.Close();
            button1.Enabled = true;
            MessageBox.Show("["+((HttpWebResponse)response).StatusDescription+ "] Data was sent", "Result", MessageBoxButtons.OK);
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Renci.SshNet;
using System.Net;
using IPAddressManagement;
using FilePathManagement;
using RemoteManagement;
using UserManagement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ExtPkgUpdateTool
{
    public partial class Form1 : Form
    {
        IPAddressOp duIpOp = new IPAddressOp("DuIpSet.cfg", "Type.cfg");
        IPAddressOp ruIpOp = new IPAddressOp("RuIpSet.cfg", "Type.cfg");
        IPAddressOp serverIpOp = new IPAddressOp("ServerIpSet.cfg", "Type.cfg");
        FilePathOp filePathOp = new FilePathOp();
        UserManager usrMng = new UserManager("UserMng.cfg");
        public Form1()
        {
            InitializeComponent();

            filePath.Text = filePathOp.GetLastSelectedPath();

            TypeSelBox.Items.Add("vDU-ECPRI");
            TypeSelBox.Items.Add("CDU-CPRI");
            TypeSelBox.SelectedIndex = duIpOp.GetDuOrRuType();

            ComboBox_Refresh(DuIpComboBox, duIpOp, duIpOp.GetIPAddressCount() - 1);
            ComboBox_Refresh(RuIpComboBox, ruIpOp, ruIpOp.GetIPAddressCount() - 1);
        }

        private void filePathSel_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Title = "选择要升级的EXT文件";
            fileDialog.Filter = "EXT Files(*.EXT)|*.EXT";
            fileDialog.InitialDirectory = filePathOp.GetLastSelectedPath();

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                string selectedFilePath = fileDialog.FileName;
                filePath.Text = selectedFilePath;
                filePathOp.SaveLastSelectedPath(selectedFilePath);
            }
        }
        
        private void uploadButton_Click(object sender, EventArgs e)
        {
            // Save the IP address and refresh the ComboBox
            string duIpAddress = DuIpComboBox.Text;
            string ruIpAddress = RuIpComboBox.Text;

            if (!duIpOp.IsIPAddressValid(duIpAddress))
            {
                MessageBox.Show("DU IP 地址无效！");
                return;
            }
            else if(!ruIpOp.IsIPAddressValid(ruIpAddress))
            {
                MessageBox.Show("RU IP 地址无效！");
                return;
            }
            else
            {
                duIpOp.SaveIPAddressToFile(duIpAddress);
                ruIpOp.SaveIPAddressToFile(ruIpAddress);
                duIpOp.SaveDevType(TypeSelBox.SelectedIndex);
                ComboBox_Refresh(DuIpComboBox, duIpOp, duIpOp.GetIPAddressCount() - 1);
                ComboBox_Refresh(RuIpComboBox, ruIpOp, ruIpOp.GetIPAddressCount() - 1);
            }

            // Start update procedure
            // 1.Put file to 116.8 server
            var testUser = usrMng.GetUserByType("testUser");
            var user1168fw = usrMng.GetUserByType("1168fw");
            var userCduUser = usrMng.GetUserByType("cduuser");
            var userCduRoot = usrMng.GetUserByType("cduroot");
            var userRuUser = usrMng.GetUserByType("cduuser");
            var userRuRoot = usrMng.GetUserByType("cduroot");
            //SshOp serverSshOp = new SshOp(serverIpOp.GetLastIpAddress(), testUser.Username, testUser.Password);
            SshOp serverSshOp = new SshOp(serverIpOp.GetLastIpAddress(), user1168fw.Username, user1168fw.Password);
            SshOp duSshOp = new SshOp(duIpAddress, userCduUser.Username, userCduUser.Password);
            SshOp ruSshOp = new SshOp(ruIpAddress, userRuUser.Username, userRuUser.Password);

            //SftpOp serverSftpOp = new SftpOp(serverIpOp.GetLastIpAddress(), testUser.Username, testUser.Password);
            SftpOp serverSftpOp = new SftpOp(serverIpOp.GetLastIpAddress(), user1168fw.Username, user1168fw.Password);
            SftpOp duSftpOp = new SftpOp(duIpAddress, userCduUser.Username, userCduUser.Password);
            SftpOp ruSftpOp = new SftpOp(ruIpAddress, userRuUser.Username, userRuUser.Password);

            //string filePath = "/home/zw/" + Environment.UserName + "/";
            string filePath = "/home/" + user1168fw.Username + "/" + Environment.UserName + "/";
            string fileTempName = filePathOp.getSelFileName() + DateTime.Now.TimeOfDay;
            //升级时需要检查升级文件是否存在！
            //每一句命令都需要检查返回值
            if (true == serverSshOp.Connect())
            {
                serverSshOp.RunCommand("mkdir -p " + filePath);
                serverSshOp.Disconnect();
            }
            if (true == serverSftpOp.Connect())
            {
                serverSftpOp.UploadFile(filePathOp.GetLastSelectedPath(), filePath + fileTempName);
                serverSftpOp.Disconnect();
            }
            if (true == serverSshOp.Connect())
            {
                if (true == duSftpOp.Connect())
                {
                    duSftpOp.UploadFile(filePath + fileTempName, "/home/" + userCduUser.Username + "/" + fileTempName);
                    duSftpOp.Disconnect();
                }
                if (true == duSshOp.Connect())
                {
                    if (true == duSshOp.StartShell())
                    {
                        duSshOp.ExecuteCommand("su -");
                        duSshOp.WaitForOutput("Password");
                        duSshOp.ExecuteCommand(userCduRoot.Password);
                        duSshOp.WaitForOutput("root");
                        duSshOp.ExecuteCommand("vrctl 31 bash");
                        duSshOp.WaitForOutput("root");
                        duSshOp.ExecuteCommand("sftp user@" + ruIpAddress);
                        duSshOp.WaitForOutput("Password");
                        duSshOp.ExecuteCommand(userRuUser.Password);
                        duSshOp.WaitForOutput("user");
                        duSshOp.ExecuteCommand("su -");
                        duSshOp.WaitForOutput("Password");
                        duSshOp.ExecuteCommand(userRuRoot.Password);
                        duSshOp.WaitForOutput("root");
                        duSshOp.ExecuteCommand("touch /home/user/LogInRuSuccess!");
                    }
                    duSshOp.Disconnect();
                }
                
                /*if (true == serverSshOp.StartShell())
                {
                    serverSshOp.ExecuteCommand("touch /home/zw/loggin");
                    if (true == serverSshOp.WaitForOutput("123"))
                    {
                        MessageBox.Show("找到了！");
                    }
                    else
                    {
                        MessageBox.Show("没找到！");
                    }
                    serverSshOp.ExecuteCommand("su -");
                    if (true == serverSshOp.WaitForOutput("Password:"))
                    {
                        serverSshOp.ExecuteCommand("1");
                        if (true == serverSshOp.WaitForOutput("root"))
                        {
                            serverSshOp.ExecuteCommand("touch /home/zw/test123");
                        }
                    }
                    
                }*/
                serverSshOp.Disconnect();
            }
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void ComboBox_Refresh(System.Windows.Forms.ComboBox comboBox, IPAddressOp ipAddrOp, int defaultIpIndex)
        {
            comboBox.Items.Clear();
            for (int index = 0; index < ipAddrOp.GetIPAddressCount(); index++)
            {
                comboBox.Items.Add(ipAddrOp.GetIPAddressAtIndex(index));
            }
            if(defaultIpIndex >= ipAddrOp.GetIPAddressCount())
                defaultIpIndex = ipAddrOp.GetIPAddressCount() - 1;
            comboBox.SelectedItem = ipAddrOp.GetIPAddressAtIndex(defaultIpIndex);
        }

        private void duIpDelButton_Click(object sender, EventArgs e)
        {
            duIpOp.DeleteIPAddress(DuIpComboBox.Text);
            ComboBox_Refresh(DuIpComboBox, duIpOp, DuIpComboBox.SelectedIndex);
        }

        private void ruIpDelButton_Click(object sender, EventArgs e)
        {
            ruIpOp.DeleteIPAddress(RuIpComboBox.Text);
            ComboBox_Refresh(RuIpComboBox, ruIpOp, RuIpComboBox.SelectedIndex);
        }

        private void DuIpComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void RuIpComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}

namespace IPAddressManagement
{
    public class IPAddressOp
    {
        private string ipFilePath;
        private string devTypeFilePath;

        public IPAddressOp(string ipFilePath, string devTypeFilePath)
        {
            this.ipFilePath = ipFilePath;
            this.devTypeFilePath = devTypeFilePath;
        }

        public bool IsIPAddressValid(string ipAddress)
        {
            IPAddress parsedIPAddress;
            // Try parse IP address
            if (IPAddress.TryParse(ipAddress, out parsedIPAddress))
            {
                // Check the validation of IPv4 or IPv6 address
                return parsedIPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    || parsedIPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            }
            return false;
        }

        public void SaveIPAddressToFile(string ipAddress)
        {
            if (!File.Exists(ipFilePath))
            {
                File.WriteAllText(ipFilePath, ipAddress);
                return;
            }
            List<string> ipAddresses = LoadIPAddressesFromFile();
            if (ipAddresses.Contains(ipAddress))
                ipAddresses.Remove(ipAddress);
            ipAddresses.Add(ipAddress);
            File.WriteAllLines(ipFilePath, ipAddresses);
            Console.WriteLine("IP 地址已保存到文件");
        }

        public void SaveDevType(int Type)
        {
            if (File.Exists(devTypeFilePath))
            {
                // Read all lines from the file
                string[] lines = File.ReadAllLines(devTypeFilePath);

                // Replace the first line with the number
                lines[0] = Type.ToString();

                // Write the modified lines back to the file
                File.WriteAllLines(devTypeFilePath, lines);
            }
            else
            {
                // Create a new file and write the number to it
                using (StreamWriter writer = File.CreateText(devTypeFilePath))
                {
                    writer.WriteLine(Type.ToString());
                }
            }
        }

        public int GetDuOrRuType()
        {
            if (!File.Exists(devTypeFilePath))
            {
                return 0;
            }
            string[] lines = File.ReadAllLines(devTypeFilePath);
            if (lines.Length > 0 && int.TryParse(lines[0], out int Type))
            {
                return Type;
            }

            return 0;
        }

        public string GetLastIpAddress()
        {
            if(!File.Exists(ipFilePath))
            {
                return "0.0.0.0";
            }
            string[] lines = File.ReadAllLines(ipFilePath);
            string lastIPAddress = lines[lines.Length - 1];
            if (string.IsNullOrEmpty(lastIPAddress))
                return "0.0.0.0";
            if (!IsIPAddressValid(lastIPAddress))
                return "0.0.0.0";
            return lastIPAddress;
        }

        public int GetIPAddressCount()
        {
            if (!File.Exists(ipFilePath))
            {
                return 0;
            }
            string[] lines = File.ReadAllLines(ipFilePath);
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (IsIPAddressValid(lines[i]))
                {
                    count++;
                }
            }
            return count;
        }

        public string GetIPAddressAtIndex(int index)
        {
            if (!File.Exists(ipFilePath))
            {
                return "0.0.0.0";
            }
            string[] lines = File.ReadAllLines(ipFilePath);
            if (index >= 0 && index < lines.Length)
            {
                string ipAddress = lines[index];
                return ipAddress;
            }
            return "0.0.0.0"; // or you can throw an exception to indicate an invalid index
        }

        public void DeleteIPAddress(string ipAddress)
        {
            List<string> ipAddresses = LoadIPAddressesFromFile();
            ipAddresses.Remove(ipAddress);
            File.WriteAllLines(ipFilePath, ipAddresses);
        }

        private List<string> LoadIPAddressesFromFile()
        {
            if (!File.Exists(ipFilePath))
            {
                return new List<string>();
            }
            return new List<string>(File.ReadAllLines(ipFilePath));
        }
    }
}

namespace FilePathManagement
{
    public class FilePathOp
    {
        public FilePathOp()
        {
        }
        public string GetLastSelectedPath()
        {
            string configFilePath = "EXTSelPath.cfg";
            string lastSelectedPath = string.Empty;

            if (File.Exists(configFilePath))
            {
                lastSelectedPath = File.ReadAllText(configFilePath);
            }

            return lastSelectedPath;
        }
        public void SaveLastSelectedPath(string selectedFilePath)
        {
            string configFilePath = "EXTSelPath.cfg";
            File.WriteAllText(configFilePath, selectedFilePath);
        }
        public string getSelFileName()
        {
            string configFilePath = GetLastSelectedPath();
            int lastIndex = configFilePath.LastIndexOf('\\');
            if (lastIndex >= 0)
            {
                return configFilePath.Substring(lastIndex + 1);
            }
            else
            {
                return null;
            }
        }
    }
}

namespace RemoteManagement
{
    public class SshOp
    {
        private readonly string host;
        private readonly string username;
        private readonly string password;
        private SshClient sshClient;
        private ShellStream shellStream;

        public SshOp(string host, string username, string password)
        {
            this.host = host;
            this.username = username;
            this.password = password;
        }

        public bool Connect()
        {
            sshClient = new SshClient(host, username, password);

            try
            {
                sshClient.Connect();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to SSH server: {ex.Message}");
                return false;
            }
        }

        public bool StartShell()
        {
            if (sshClient == null || !sshClient.IsConnected)
            {
                Console.WriteLine("SSH client is not connected.");
                return false;
            }

            try
            {
                shellStream = sshClient.CreateShellStream("xterm", 80, 24, 800, 600, 1024);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start SSH shell: {ex.Message}");
                return false;
            }
        }

        public void ExecuteCommand(string command)
        {
            if (shellStream == null || !shellStream.CanWrite)
            {
                Console.WriteLine("SSH shell is not available.");
                return;
            }

            try
            {
                shellStream.WriteLine(command);
                shellStream.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute SSH command: {ex.Message}");
            }
        }

        public string RunCommand(string command)
        {
            if (sshClient == null || !sshClient.IsConnected)
            {
                Console.WriteLine("SSH client is not connected.");
                return null;
            }

            try
            {
                var sshCommand = sshClient.RunCommand(command);
                var result = sshCommand.Result;
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to run SSH command: {ex.Message}");
                return null;
            }
        }

        public bool WaitForOutput(string expectedOutput)
        {
            if (shellStream == null || !shellStream.CanRead)
            {
                Console.WriteLine("SSH shell is not available for reading.");
                return false;
            }

            try
            {
                var outputBuffer = new StringBuilder();
                var buffer = new byte[1024];

                while (true)
                {
                    var bytesRead = shellStream.Read(buffer, 0, buffer.Length);
                    /*if (bytesRead <= 0)
                    {
                        Console.WriteLine("bytesRead <= 0");
                        break;
                    }*/

                    // 字节流转换为字符串
                    var output = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    outputBuffer.Append(output);

                    // 检查是否达到预期的输出
                    if (outputBuffer.ToString().Contains(expectedOutput))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to wait for SSH output: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            if (sshClient != null && sshClient.IsConnected)
            {
                sshClient.Disconnect();
            }
        }
    }
    public class SftpOp
    {
        private readonly string host;
        private readonly string username;
        private readonly string password;
        private SftpClient sftpClient;

        public SftpOp(string host, string username, string password)
        {
            this.host = host;
            this.username = username;
            this.password = password;
        }

        public bool Connect()
        {
            sftpClient = new SftpClient(host, username, password);

            try
            {
                sftpClient.Connect();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to SFTP server: {ex.Message}");
                return false;
            }
        }

        public bool DownloadFile(string remoteFilePath, string localFilePath)
        {
            if (sftpClient == null || !sftpClient.IsConnected)
            {
                Console.WriteLine("SFTP client is not connected.");
                return false;
            }

            try
            {
                using (var fileStream = new FileStream(localFilePath, FileMode.Create))
                {
                    sftpClient.DownloadFile(remoteFilePath, fileStream);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to download file from SFTP server: {ex.Message}");
                return false;
            }
        }

        public bool UploadFile(string localFilePath, string remoteFilePath)
        {
            if (sftpClient == null || !sftpClient.IsConnected)
            {
                Console.WriteLine("SFTP client is not connected.");
                return false;
            }

            try
            {
                using (var fileStream = new FileStream(localFilePath, FileMode.Open))
                {
                    sftpClient.UploadFile(fileStream, remoteFilePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to upload file to SFTP server: {ex.Message}" + remoteFilePath);
                return false;
            }
        }

        public void RenameFile(string oldRemoteFilePath, string newRemoteFilePath)
        {
            sftpClient.RenameFile(oldRemoteFilePath, newRemoteFilePath);
        }

        public void Disconnect()
        {
            if (sftpClient != null && sftpClient.IsConnected)
            {
                sftpClient.Disconnect();
            }
        }
    }
}

namespace UserManagement
{
    public class User
    {
        public string Type { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public User(string type, string username, string password)
        {
            Type = type;
            Username = username;
            Password = password;
        }

        public override string ToString()
        {
            return $"{Type}:{Username}:{Password}";
        }
    }

    public class UserManager
    {
        private string filePath;
        private Dictionary<string, User> users;

        public UserManager(string filePath)
        {
            this.filePath = filePath;
            users = new Dictionary<string, User>();
            LoadUsersFromFile();
        }

        public void AddUser(string type, string username, string password)
        {
            var user = new User(type, username, password);
            string userString = user.ToString();

            users[username] = user;

            // Write all users to the file
            File.WriteAllLines(filePath, GetUsersAsStringList());
        }

        public void RemoveUser(string username)
        {
            if (users.ContainsKey(username))
            {
                users.Remove(username);

                // Write all users to the file
                File.WriteAllLines(filePath, GetUsersAsStringList());
            }
        }

        public User GetUserByType(string type)
        {
            foreach (var user in users.Values)
            {
                if (user.Type == type)
                {
                    return user;
                }
            }

            return null;
        }

        private List<string> GetUsersAsStringList()
        {
            var userList = new List<string>();

            foreach (var user in users.Values)
            {
                userList.Add(user.ToString());
            }

            return userList;
        }

        private void LoadUsersFromFile()
        {
            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    string[] parts = line.Split(':');
                    if (parts.Length == 3)
                    {
                        string type = parts[0];
                        string username = parts[1];
                        string password = parts[2];
                        var user = new User(type, username, password);
                        users[username] = user;
                    }
                }
            }
        }
    }
}
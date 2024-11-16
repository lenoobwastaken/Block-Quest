using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VRChat.API;
using VRChat.API.Api;
using VRChat.API.Client;
using VRChat.API.Model;
namespace Anti_Quest
{
    internal class Program
    {
        static List<string> UserDB = new List<string>();

        static List<string> PlayerQueue = new List<string>();
        static Configuration config;
        static ApiClient client;
        static AuthenticationApi authApi;
        static UsersApi userApi;
        static PlayermoderationApi modapi;
        static string userdbfile = Environment.CurrentDirectory + "\\UserDB.txt";

        public static void Main(string[] args)
        {
            Init();
        }
        static void Init()
        {
            if (!System.IO.File.Exists(userdbfile))
            {
                System.IO.File.Create(userdbfile);
            }
            if (string.IsNullOrEmpty(System.IO.File.ReadAllText(userdbfile)) != true)
            {
                foreach (var line in System.IO.File.ReadAllLines(userdbfile))
                {
                    UserDB.Add(line);
                }
            }
            config = new Configuration();
            client = new ApiClient();
            config.UserAgent = "AntiQ/0.0.1 AntiQ";
            if (string.IsNullOrEmpty(System.IO.File.ReadAllText(Environment.CurrentDirectory + "\\Login.txt")) == true)
            {
                Console.WriteLine("Please put your current vrc login in the Login.txt");
                Console.ReadKey();
                Environment.Exit(0);
            }
            var loginFile = System.IO.File.ReadAllLines(Environment.CurrentDirectory + "\\Login.txt");
            var login = loginFile.First().Split(':');

            config.Username = login[0];
            config.Password = login[1];
            authApi = new AuthenticationApi(client, client, config); 
            userApi = new UsersApi(client, client, config);
            modapi = new PlayermoderationApi(config);
            ApiResponse<CurrentUser> currentUserResp = authApi.GetCurrentUserWithHttpInfo();
            if (currentUserResp.RawContent.Contains("emailOtp"))
            {
                Console.WriteLine("enter 2fa code");
                var code = Console.ReadLine();
                authApi.Verify2FAEmailCode(new TwoFactorEmailCode(code));

            }
            CurrentUser currentUser = authApi.GetCurrentUser();
            Console.WriteLine($"Logged in as {currentUser.DisplayName}");
            Console.WriteLine(currentUser.LastPlatform);
            CheckLog();
        }
        static System.DateTime lastfilecreationdate = DateTime.MinValue;
        static string currentlog = "";
        static bool islogging = false;
        static string userpattern = @"usr_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
        static Task CheckLog()
        {
            while (true)
            {
                string pattern = @"output_log_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}";
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace("Roaming", "LocalLow") + "\\VRChat\\VRChat\\";

                foreach (var file in System.IO.Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace("Roaming", "LocalLow") + "\\VRChat\\VRChat\\"))
                {
                    if (file.EndsWith(".txt") && Regex.IsMatch(file, pattern) == true)
                    {
                        var time = System.IO.File.GetCreationTime(file);
                        if (time > lastfilecreationdate)
                        {
                            lastfilecreationdate = time;
                            currentlog = file;
                        }

                    }

                }
                
                string outputlog = "";
                StreamReader sr;
                var Copied = currentlog.Replace("output_log", "antiq");
                

                if (System.IO.File.Exists(Copied))
                {
                    System.IO.File.Delete(Copied);
                }
                System.IO.File.Copy(currentlog, Copied);

                using (FileStream stream = new FileStream(Copied, FileMode.Open, FileAccess.Read, FileShare.Read))
                {


                    using (sr = new StreamReader(stream))
                    {
                        while ((outputlog = sr.ReadLine()) != null)
                        {
                            if (outputlog.Contains("OnPlayerJoined"))
                            {
                                string usr = Regex.Match(outputlog, userpattern).Value;
                                if (usr != null && !UserDB.Contains(usr))
                                {
                                    Console.WriteLine("Checking: " + usr);
                                    var apiusr = userApi.GetUser(usr);
                                    if (apiusr != null && userApi.GetUser(usr).LastPlatform == "android")
                                    {
                                        var userreq = new ModerateUserRequest(usr);
                                      
                                     
                                        userreq.Type = PlayerModerationType.Block;

                                        modapi.ModerateUser(userreq);
                                        Console.WriteLine("Blocked: " + apiusr.DisplayName);
                                        Thread.Sleep(1000);
                                    }
                                    UserDB.Add(usr);
                                    var sb = new StringBuilder();
                                    foreach (var lines in UserDB)
                                    {
                                        sb.AppendLine(lines + "\n");
                                    }
                                    System.IO.File.WriteAllText(userdbfile, sb.ToString());
                                }

                            }
                        }
                        sr.Close();
                    }
                    stream.Close();
                }

            }
            return Task.Delay(100);

        }

    }
}

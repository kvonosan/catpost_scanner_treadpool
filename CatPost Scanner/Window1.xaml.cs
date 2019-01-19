using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

namespace CatPost_Scanner
{
    class Attachment
    {
        public int post_id;
        public string type;

        //photo
        public int vk_id;
        public int owner_id;
        public string link;
        public long date;

        //doc ext=gif
        //vk_id
        //owner_id
        //link
    }

    class Ids
    {
        public int Id;
        public string Name;
        public string type;
        public int Count;
    }

    /// <summary>
    /// Логика взаимодействия для Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        private MainWindow window;
        private List<Ids> ids = new List<Ids>();
        private int tasks_count = 100000; // по сколько постов на таск
        private int scanned = 0;
        private int post_count = 0;
        private List<Attachment> attachments = new List<Attachment>();
        private object locker = new object();

        private void AllowUIToUpdate()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(delegate (object parameter)
            {
                frame.Continue = false;
                return null;
            }), null);
            Dispatcher.PushFrame(frame);
        }

        public Window1(MainWindow win)
        {
            window = win;
            InitializeComponent();
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private int GetGroupId(string url)
        {
            url = url.TrimEnd('/');
            string[] splitted = url.Split('/');
            int len = splitted.Length;
            var GroupName = splitted[len-1];

            WebClient client = new WebClient();
            Stream data = client.OpenRead("https://api.vk.com/method/groups.getById?" + window.Token + "&group_id=" + GroupName + "&v=" + window.version);
            StreamReader reader = new StreamReader(data);
            JObject groups = JObject.Parse(reader.ReadToEnd());
            reader.Close();
            data.Close();

            if (groups.SelectToken("response") != null)
            {
                return int.Parse(groups["response"][0]["id"].ToString());
            }

            return -1;
        }

        private int GetUserId(string url)
        {
            url = url.TrimEnd('/');
            string[] splitted = url.Split('/');
            int len = splitted.Length;
            var UserName = splitted[len - 1];

            WebClient client = new WebClient();
            Stream data = client.OpenRead("https://api.vk.com/method/users.get?" + window.Token + "&user_ids=" + UserName + "&v=" + window.version);
            StreamReader reader = new StreamReader(data);
            JObject Users = JObject.Parse(reader.ReadToEnd());
            reader.Close();
            data.Close();

            if (Users.SelectToken("response") != null)
            {
                return int.Parse(Users["response"][0]["id"].ToString());
            }

            return -1;
        }

        private int GetCount(int id, string type)
        {
            WebClient client = new WebClient();
            int scan_id = id;
            if (type == "group")
            {
                scan_id = -1 * scan_id;
            }
            Stream data = client.OpenRead("https://api.vk.com/method/wall.get?" + window.Token + "&owner_id=" + scan_id + "&count=1&v=" + window.version);
            StreamReader reader = new StreamReader(data);
            JObject contents = JObject.Parse(reader.ReadToEnd());
            reader.Close();
            data.Close();

            if (contents.SelectToken("response") != null)
            {
                return int.Parse(contents["response"]["count"].ToString());
            }

            return -1;
        }

        private async Task<int> GetCountTask(int id, string type)
        {
            return await Task.Run<int>(() =>
            {
                return GetCount(id, type);
            });
        }

        private async Task<int> GetGroupIdTask(string url)
        {
            return await Task.Run<int>(() =>
            {
                return GetGroupId(url);
            });
        }

        private async Task<int> GetUserIdTask(string url)
        {
            return await Task.Run<int>(() =>
            {
                return GetUserId(url);
            });
        }

        private async Task GetIdsTask()
        {
            button.IsEnabled = false;
            button1.IsEnabled = false;
            tabControl.SelectedIndex = 1;

            ids.Clear();
            TextBox1.AppendText("---------------------------------------------\n");
            TextBox1.AppendText("Текущий пользователь " + window.User + "!\n");

            int lineCount = TextBox.LineCount;
            for (int line = 0; line < lineCount; line++)
            {
                string str = TextBox.GetLineText(line).Replace(Environment.NewLine, "");
                if (str != "")
                {
                    Ids id = new Ids();
                    int gId = await GetGroupIdTask(str);
                    if (gId != -1)
                    {
                        id.Id = gId;
                        id.type = "group";
                        id.Name = str;
                        if (!idExist(gId))
                        {
                            int post_counts = await GetCountTask(gId, "group");
                            if (post_counts != -1)
                            {
                                id.Count = post_counts;
                                ids.Add(id);
                                TextBox1.AppendText("Группа " + id.Name + " (id = " + id.Id + ") имеет " + id.Count + " постов. Группа добавлена в очередь.\n");
                            }
                            else
                            {
                                TextBox1.AppendText("Группа " + id.Name + " (id = " + id.Id + ") - ошибка взятия количества постов. Группа не добавлена.\n");
                            }
                        }
                        else
                        {
                            TextBox1.AppendText("Группа " + id.Name + " (id = " + id.Id + ") - уже в очереди сканирования.\n");
                        }
                    }
                    else
                    {
                        int UserId = await GetUserIdTask(str);
                        if (UserId != -1)
                        {
                            id.Id = UserId;
                            id.type = "user";
                            id.Name = str;
                            if (!idExist(UserId))
                            {
                                int post_counts = await GetCountTask(UserId, "user");
                                if (post_counts != -1)
                                {
                                    id.Count = post_counts;
                                    ids.Add(id);
                                    TextBox1.AppendText("Пользователь  " + id.Name + " (id = " + id.Id + ") имеет " + id.Count + " постов. Пользователь добавлен в очередь.\n");
                                }
                                else
                                {
                                    TextBox1.AppendText("Пользователь " + str + " (id = " + UserId + ") - ошибка взятия количества постов.\n");
                                }
                            }
                            else
                            {
                                TextBox1.AppendText("Пользователь " + id.Name + " (id = " + id.Id + ") - уже в очереди сканирования.\n");
                            }
                        }
                        else
                        {
                            TextBox1.AppendText("Пользователь или группа" + str + " не существует.\n");
                        }
                    }
                }
            }
            TextBox1.AppendText("Очередь групп и пользователей создана! Всего в сканировании " + ids.Count + ".\n");
        }

        private bool idExist(int Id)
        {
            foreach (Ids id in ids)
            {
                if (id.Id == Id)
                {
                    return true;
                }
            }
            return false;
        }

        private void WorkTask()
        {
            scanned = 0;
            post_count = 0;
            List<Task> listoftasks = new List<Task>();
            foreach (Ids id in ids)
            {
                if (id.type == "group")
                {
                    TextBox1.AppendText("Сканируем группу http://vk.com/club" + id.Id + ".\n");
                }
                else
                {
                    TextBox1.AppendText("Сканируем пользователя http://vk.com/id" + id.Id + ".\n");
                }
                TextBox1.AppendText("Всего записей " + id.Count + ".\n");
                MySqlConnection conn = new MySqlConnection("server=185.159.129.209;user=root;database=catpost_content_vk;password=test1234;Character Set=utf8mb4;");
                conn.Open();

                string sql = "SELECT vk_id FROM groups_and_users WHERE type=@type AND vk_id=@id";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@type", id.type);
                cmd.Parameters.AddWithValue("@id", id.Id);
                MySqlDataReader reader = cmd.ExecuteReader();
                bool post_exist_in_database = false;
                while (reader.Read())
                {
                    post_exist_in_database = true;
                }
                reader.Close();
                if (!post_exist_in_database)
                {
                    string insert = "INSERT INTO groups_and_users (vk_id, type, name) VALUES(@id, @type, @name);";
                    MySqlCommand cmd1 = new MySqlCommand(insert, conn);
                    cmd1.Prepare();
                    cmd1.Parameters.AddWithValue("@id", id.Id);
                    cmd1.Parameters.AddWithValue("@type", id.type);
                    cmd1.Parameters.AddWithValue("@name", id.Name);
                    cmd1.ExecuteNonQuery();
                }

                int taskCount = id.Count / tasks_count + 1;
                TextBox1.AppendText("Сканируем...\n");
                Dispatcher.CurrentDispatcher.Invoke(new Action(delegate { }), DispatcherPriority.Background);

                for (int i = 0; i < taskCount; i++)
                {
                    int global_offset1 = i * tasks_count;
                    int scanid = id.Id;
                    string type = id.type;
                    int count = 0;
                    if (i + 1 == taskCount)
                    {
                        count = id.Count % tasks_count;
                    }
                    else
                    {
                        count = tasks_count;
                    }
                    if (id.type == "user")
                    {
                        listoftasks.Add(new Task(() => { Scan(id.Id, global_offset1, count); }));// заккоменть чтобы было без пула
                        //Scan(id.Id, global_offset1, count);// без пула
                    }
                    else
                    {
                        listoftasks.Add(new Task(() => { Scan(-1 * id.Id, global_offset1, count); }));// заккоменть чтобы было без пула
                        //Scan(-1 * id.Id, global_offset1, count);// без пула
                    }
                    scanned += count;
                }
                StartAndWaitAllThrottled(listoftasks, 10);// заккоменть чтобы было без пула
                TextBox1.AppendText("Просканировано " + scanned + " добавлено в базу " + post_count + " всего постов " + id.Count + ".\n");
                listoftasks.Clear();
            }
            TextBox1.AppendText("<--- Сканирование завершено " + DateTime.Now.ToString() + ".\n");
        }

        /// <summary>
        /// Starts the given tasks and waits for them to complete. This will run, at most, the specified number of tasks in parallel.
        /// <para>NOTE: If one of the given tasks has already been started, an exception will be thrown.</para>
        /// </summary>
        /// <param name="tasksToRun">The tasks to run.</param>
        /// <param name="maxTasksToRunInParallel">The maximum number of tasks to run in parallel.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public static void StartAndWaitAllThrottled(IEnumerable<Task> tasksToRun, int maxTasksToRunInParallel, CancellationToken cancellationToken = new CancellationToken())
        {
            StartAndWaitAllThrottled(tasksToRun, maxTasksToRunInParallel, -1, cancellationToken);
        }

        /// <summary>
        /// Starts the given tasks and waits for them to complete. This will run, at most, the specified number of tasks in parallel.
        /// <para>NOTE: If one of the given tasks has already been started, an exception will be thrown.</para>
        /// </summary>
        /// <param name="tasksToRun">The tasks to run.</param>
        /// <param name="maxTasksToRunInParallel">The maximum number of tasks to run in parallel.</param>
        /// <param name="timeoutInMilliseconds">The maximum milliseconds we should allow the max tasks to run in parallel before allowing another task to start. Specify -1 to wait indefinitely.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public static void StartAndWaitAllThrottled(IEnumerable<Task> tasksToRun, int maxTasksToRunInParallel, int timeoutInMilliseconds, CancellationToken cancellationToken = new CancellationToken())
        {
            // Convert to a list of tasks so that we don&#39;t enumerate over it multiple times needlessly.
            var tasks = tasksToRun.ToList();

            using (var throttler = new SemaphoreSlim(maxTasksToRunInParallel))
            {
                var postTaskTasks = new List<Task>();

                // Have each task notify the throttler when it completes so that it decrements the number of tasks currently running.
                tasks.ForEach(t => postTaskTasks.Add(t.ContinueWith(tsk => throttler.Release())));

                // Start running each task.
                foreach (var task in tasks)
                {
                    // Increment the number of tasks currently running and wait if too many are running.
                    throttler.Wait(timeoutInMilliseconds, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    task.Start();
                }

                // Wait for all of the provided tasks to complete.
                // We wait on the list of "post" tasks instead of the original tasks, otherwise there is a potential race condition where the throttler&#39;s using block is exited before some Tasks have had their "post" action completed, which references the throttler, resulting in an exception due to accessing a disposed object.
                Task.WaitAll(postTaskTasks.ToArray(), cancellationToken);
            }
        }

        private async Task button_work()
        {
            button.IsEnabled = false;
            button1.IsEnabled = false;
            await GetIdsTask();
            TextBox1.AppendText("---> Сканирование запущено " + DateTime.Now.ToString() + ".\n");
            WorkTask();
            button.IsEnabled = true;
            button1.IsEnabled = true;
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            await button_work();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            TextBox1.Clear();
            button1.IsEnabled = false;
        }

        private void TextBox1_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            TextBox1.ScrollToEnd();
            AllowUIToUpdate();
        }

        private void Scan(int id, int global_offset, int count)
        {
            int ciclov = count / 100;
            int post_added_count = 0;

            for (var i = 0; i <= ciclov; i++)
            {
                int offset = global_offset + i * 100;
                int current_count = 100;
                if (i == ciclov)
                {
                    current_count = count - i * 100;
                }

                WebClient client = new WebClient();
                Stream data = client.OpenRead("https://api.vk.com/method/wall.get?" + window.Token + "&owner_id=" + id + "&offset=" + offset + "&count=" + current_count + "&v=" + window.version);
                StreamReader reader = new StreamReader(data);
                JObject contents = JObject.Parse(reader.ReadToEnd());

                MySqlConnection conn = new MySqlConnection("server=185.159.129.209;user=root;database=catpost_content_vk;password=test1234;Character Set=utf8mb4;");
                conn.Open();
                string sql1 = "SELECT id FROM posts_vk WHERE vk_id=@vk_id";
                MySqlCommand post_exist = new MySqlCommand(sql1, conn);
                post_exist.Prepare();
                string sql2 = "INSERT INTO posts_vk (vk_id, date, text, likes, reposts, views, repost_text, owner_id, who_add, trash)" +
                    " VALUES(@vk_id, @date, @text, @likes, @reposts, @views, @repost_text, @owner_id, @who_add, 0)";
                MySqlCommand post_insert = new MySqlCommand(sql2, conn);
                post_insert.Prepare();
                string sql3 = "INSERT INTO attachments_vk (post_id, type, vk_id, owner_id, link, date)" +
                            " VALUES(@post_id, @type, @vk_id, @owner_id, @link, @date)";
                MySqlCommand attach_insert = new MySqlCommand(sql3, conn);
                attach_insert.Prepare();
                string sql4 = "SELECT vk_id FROM attachments_vk WHERE type=@type AND vk_id=@vk_id AND post_id=@post_id";
                MySqlCommand attach_exist = new MySqlCommand(sql4, conn);
                attach_exist.Prepare();

                lock (locker)
                {
                    if (contents.SelectToken("response") != null)
                    {
                        for (var j = 0; j < current_count; j++)
                        {
                            bool legal_post = true;
                            int vk_id = int.Parse(contents["response"]["items"][j]["id"].ToString());
                            long date = long.Parse(contents["response"]["items"][j]["date"].ToString());
                            string text = contents["response"]["items"][j]["text"].ToString();
                            int likes = int.Parse(contents["response"]["items"][j]["likes"]["count"].ToString());
                            int reposts = int.Parse(contents["response"]["items"][j]["reposts"]["count"].ToString());
                            int marked_as_ads = 0;
                            string repost_text = "";

                            if (contents["response"]["items"][j].SelectToken("marked_as_ads") != null)
                            {
                                marked_as_ads = int.Parse(contents["response"]["items"][j]["marked_as_ads"].ToString());
                            }
                            int owner_id = int.Parse(contents["response"]["items"][j]["owner_id"].ToString());
                            int views = 0;
                            if (contents["response"]["items"][j].SelectToken("views") != null)
                            {
                                views = int.Parse(contents["response"]["items"][j]["views"]["count"].ToString());
                            }

                            if (contents["response"]["items"][j].SelectToken("attachments") != null)
                            {
                                int attachments_count = contents["response"]["items"][j]["attachments"].Count();
                                for (int a = 0; a < attachments_count; a++)
                                {
                                    string type = contents["response"]["items"][j]["attachments"][a]["type"].ToString();
                                    Attachment attach = new Attachment();
                                    attach.post_id = vk_id;
                                    attach.type = type;
                                    if (type == "photo")
                                    {
                                        attach.vk_id = int.Parse(contents["response"]["items"][j]["attachments"][a]["photo"]["id"].ToString());
                                        attach.owner_id = int.Parse(contents["response"]["items"][j]["attachments"][a]["photo"]["owner_id"].ToString());
                                        if (contents["response"]["items"][j]["attachments"][a]["photo"].SelectToken("photo_1280") != null)
                                        {
                                            attach.link = contents["response"]["items"][j]["attachments"][a]["photo"]["photo_1280"].ToString();
                                        }
                                        else if (contents["response"]["items"][j]["attachments"][a]["photo"].SelectToken("photo_807") != null)
                                        {
                                            attach.link = contents["response"]["items"][j]["attachments"][a]["photo"]["photo_807"].ToString();
                                        }
                                        else if (contents["response"]["items"][j]["attachments"][a]["photo"].SelectToken("photo_604") != null)
                                        {
                                            attach.link = contents["response"]["items"][j]["attachments"][a]["photo"]["photo_604"].ToString();
                                        }
                                        else if (contents["response"]["items"][j]["attachments"][a]["photo"].SelectToken("photo_130") != null)
                                        {
                                            attach.link = contents["response"]["items"][j]["attachments"][a]["photo"]["photo_130"].ToString();
                                        }
                                        else if (contents["response"]["items"][j]["attachments"][a]["photo"].SelectToken("photo_75") != null)
                                        {
                                            attach.link = contents["response"]["items"][j]["attachments"][a]["photo"]["photo_75"].ToString();
                                        }
                                        attach.date = long.Parse(contents["response"]["items"][j]["attachments"][a]["photo"]["date"].ToString());
                                    }
                                    else if (type == "doc")
                                    {
                                        attach.vk_id = int.Parse(contents["response"]["items"][j]["attachments"][a]["doc"]["id"].ToString());
                                        attach.owner_id = int.Parse(contents["response"]["items"][j]["attachments"][a]["doc"]["owner_id"].ToString());
                                        attach.link = contents["response"]["items"][j]["attachments"][a]["doc"]["url"].ToString();
                                    }
                                    else
                                    {
                                        legal_post = false;
                                    }
                                    if (legal_post)
                                    {
                                        attachments.Add(attach);
                                    }
                                }
                            }
                            if (contents["response"]["items"][j].SelectToken("copy_history") != null)
                            {
                                repost_text = contents["response"]["items"][j]["copy_history"][0]["text"].ToString();
                                owner_id = int.Parse(contents["response"]["items"][j]["copy_history"][0]["owner_id"].ToString());

                                if (contents["response"]["items"][j]["copy_history"][0].SelectToken("attachments") != null)
                                {
                                    int copy_history_attachments = contents["response"]["items"][j]["copy_history"][0]["attachments"].Count();
                                    for (int a = 0; a < copy_history_attachments; a++)
                                    {
                                        string type = contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["type"].ToString();
                                        Attachment attach = new Attachment();
                                        attach.post_id = vk_id;
                                        attach.type = type;
                                        if (type == "photo")
                                        {
                                            attach.vk_id = int.Parse(contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"]["id"].ToString());
                                            attach.owner_id = int.Parse(contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"]["owner_id"].ToString());
                                            if (contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"].SelectToken("photo_1280") != null)
                                            {
                                                attach.link = contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"]["photo_1280"].ToString();
                                            }
                                            else if (contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"].SelectToken("photo_807") != null)
                                            {
                                                attach.link = contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"]["photo_807"].ToString();
                                            }
                                            else if (contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"].SelectToken("photo_604") != null)
                                            {
                                                attach.link = contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"]["photo_604"].ToString();
                                            }
                                            else if (contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"].SelectToken("photo_130") != null)
                                            {
                                                attach.link = contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"]["photo_130"].ToString();
                                            }
                                            else if (contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"].SelectToken("photo_75") != null)
                                            {
                                                attach.link = contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"]["photo_75"].ToString();
                                            }
                                            attach.date = long.Parse(contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["photo"]["date"].ToString());
                                        }
                                        else if (type == "doc")
                                        {
                                            attach.vk_id = int.Parse(contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["doc"]["id"].ToString());
                                            attach.owner_id = int.Parse(contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["doc"]["owner_id"].ToString());
                                            attach.link = contents["response"]["items"][j]["copy_history"][0]["attachments"][a]["doc"]["url"].ToString();
                                        }
                                        else
                                        {
                                            legal_post = false;
                                        }
                                        if (legal_post)
                                        {
                                            attachments.Add(attach);
                                        }
                                    }
                                }
                            }

                            if (marked_as_ads == 1)
                            {
                                legal_post = false;
                            }

                            if (legal_post)
                            {
                                post_exist.Parameters.Clear();
                                post_exist.Parameters.AddWithValue("@vk_id", vk_id);
                                MySqlDataReader post_exist_reader = post_exist.ExecuteReader();
                                bool post_exist_in_database = false;
                                while (post_exist_reader.Read())
                                {
                                    post_exist_in_database = true;
                                }
                                post_exist_reader.Close();
                                if (!post_exist_in_database)
                                {
                                    post_insert.Parameters.Clear();
                                    post_insert.Parameters.AddWithValue("@vk_id", vk_id);
                                    post_insert.Parameters.AddWithValue("@date", date);
                                    post_insert.Parameters.AddWithValue("@text", text);
                                    post_insert.Parameters.AddWithValue("@likes", likes);
                                    post_insert.Parameters.AddWithValue("@reposts", reposts);
                                    post_insert.Parameters.AddWithValue("@views", views);
                                    post_insert.Parameters.AddWithValue("@repost_text", repost_text);
                                    post_insert.Parameters.AddWithValue("@owner_id", owner_id);
                                    post_insert.Parameters.AddWithValue("@who_add", window.User);
                                    post_insert.ExecuteNonQuery();

                                    foreach (Attachment current_attach in attachments.ToArray())
                                    {
                                        /*attach_exist.Parameters.Clear();
                                        attach_exist.Parameters.AddWithValue("@vk_id", current_attach.vk_id);
                                        attach_exist.Parameters.AddWithValue("@type", current_attach.type);
                                        attach_exist.Parameters.AddWithValue("@post_id", current_attach.post_id);
                                        MySqlDataReader attach_exist_reader = attach_exist.ExecuteReader();
                                        bool attach_exist_in_database = false;
                                        while (attach_exist_reader.Read())
                                        {
                                            attach_exist_in_database = true;
                                        }
                                        attach_exist_reader.Close();

                                        if (!attach_exist_in_database)
                                        {*/
                                            attach_insert.Parameters.Clear();
                                            attach_insert.Parameters.AddWithValue("@post_id", current_attach.post_id);
                                            attach_insert.Parameters.AddWithValue("@type", current_attach.type);
                                            attach_insert.Parameters.AddWithValue("@vk_id", current_attach.vk_id);
                                            attach_insert.Parameters.AddWithValue("@owner_id", current_attach.owner_id);
                                            attach_insert.Parameters.AddWithValue("@link", current_attach.link);
                                            attach_insert.Parameters.AddWithValue("@date", current_attach.date);
                                            attach_insert.ExecuteNonQuery();
                                        //}
                                    }
                                    attachments.Clear();
                                    post_added_count++;
                                }
                            }
                        }
                    }
                }
            }
            post_count += post_added_count;
        }
    }
}

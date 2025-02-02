using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiApp
{
    public partial class Form1 : Form
    {
        private HttpListener listener;
        private List<User> users;
        public Form1()
        {
            InitializeComponent();
            users = new List<User>
            {
                new User { Id = 1, Email = "test1@gmail.com", Name = "John1" },
                new User { Id = 2, Email = "test2@gmail.com", Name = "John2" }
            };
        }

        private async void buttonStart_Click(object sender, EventArgs e)
        {
            if (listener == null)
            {
                listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:5000/");
                listener.Start();
                textBoxOutput.AppendText($"API запущено на http://localhost:5000/{Environment.NewLine}");

                await Task.Run(() => ListenForRequests());
            }
        }
        private async void ListenForRequests()
        {
            while (listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;
                string responseText = "";

                if (request.Url.AbsolutePath == "/favicon.ico")
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                    continue;
                }

                if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/users")
                {
                    responseText = JsonSerializer.Serialize(users);
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath.StartsWith("/users/"))
                {
                    int id = GetUserIdFromUrl(request.Url.AbsolutePath);
                    var user = users.Find(u => u.Id == id);
                    responseText = user != null ? JsonSerializer.Serialize(user) : "Користувач не знайдений";
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/users")
                {
                    var body = new System.IO.StreamReader(request.InputStream).ReadToEnd();
                    var newUser = JsonSerializer.Deserialize<User>(body);
                    if (newUser != null)
                    {
                        newUser.Id = users.Count + 1;
                        users.Add(newUser);
                        responseText = JsonSerializer.Serialize(newUser);
                    }
                }
                else if (request.HttpMethod == "DELETE" && request.Url.AbsolutePath.StartsWith("/users/"))
                {
                    int id = GetUserIdFromUrl(request.Url.AbsolutePath);
                    users.RemoveAll(u => u.Id == id);
                    responseText = "Користувач видалений";
                }
                else if (request.HttpMethod == "PATCH" && request.Url.AbsolutePath.StartsWith("/users/"))
                {
                    int id = GetUserIdFromUrl(request.Url.AbsolutePath);
                    var user = users.Find(u => u.Id == id);

                    if (user != null)
                    {
                        var body = new System.IO.StreamReader(request.InputStream).ReadToEnd();
                        var updatedUser = JsonSerializer.Deserialize<User>(body);

                        if (updatedUser != null)
                        {
                            if (!string.IsNullOrEmpty(updatedUser.Name))
                            {
                                user.Name = updatedUser.Name;
                            }
                            if (!string.IsNullOrEmpty(updatedUser.Email))
                            {
                                user.Email = updatedUser.Email;
                            }
                            responseText = JsonSerializer.Serialize(user);
                        }
                        else
                        {
                            responseText = "Помилка в оновленні користувача.";
                        }
                    }
                    else
                    {
                        responseText = "Користувач не знайдений.";
                    }
                }

                byte[] buffer = Encoding.UTF8.GetBytes(responseText);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();

                this.Invoke((MethodInvoker)delegate
                {
                    textBoxOutput.AppendText($"{request.HttpMethod} {request.Url} -> {responseText}{Environment.NewLine}");
                });
            }
        }

        private int GetUserIdFromUrl(string url)
        {
            var parts = url.Split('/');
            return int.TryParse(parts[parts.Length - 1], out int id) ? id : -1;
        }

    }
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
    }
}

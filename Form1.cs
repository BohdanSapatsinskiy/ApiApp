using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Linq;

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
                new User { Id = 1, Email = "admin@gmail.com", Name = "AdminUser", Password = "admin123", Role = "Admin" },
                new User { Id = 2, Email = "Tommy@gmail.com", Name = "Tommy", Password = "user123", Role = "User" },
                new User { Id = 3, Email = "test1@gmail.com", Name = "John1", Password = "user123", Role = "User" },
                new User { Id = 4, Email = "test2@gmail.com", Name = "John2", Password = "user123", Role = "User" }
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

                else if (request.HttpMethod == "GET")
                {
                    var userPrincipal = GetUserFromRequest(request);

                    if (userPrincipal == null)
                    {
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        responseText = "Неавторизовано";
                    }
                    else if (request.Url.AbsolutePath == "/users")
                    {
                        if (!userPrincipal.IsInRole("Admin"))
                        {
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            responseText = "Заборонено: Тільки адміністратори можуть переглядати список користувачів";
                        }
                        else
                        {
                            responseText = JsonSerializer.Serialize(users);
                        }
                    }
                    else if (request.Url.AbsolutePath.StartsWith("/users/"))
                    {
                        int id = GetUserIdFromUrl(request.Url.AbsolutePath);

                      
                        if (userPrincipal.IsInRole("Admin") || userPrincipal.Identity.Name == users.FirstOrDefault(u => u.Id == id)?.Email)  // Заміна на Email
                        {
                            var user = users.FirstOrDefault(u => u.Id == id);

                            if (user != null)
                            {
                                responseText = JsonSerializer.Serialize(user);
                            }
                            else
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                responseText = "Користувача не знайдено";
                            }
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            responseText = "Заборонено: Ви не маєте доступу до цього профілю";
                        }
                    }

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
                    var userPrincipal = GetUserFromRequest(request);
                    if (userPrincipal == null)
                    {
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        responseText = "Неавторизовано";
                    }
                    else if (!userPrincipal.IsInRole("Admin"))
                    {
                        response.StatusCode = (int)HttpStatusCode.Forbidden;
                        responseText = "Заборонено: тільки адміни можуть видаляти користувачів";
                    }
                    else
                    {
                        int id = GetUserIdFromUrl(request.Url.AbsolutePath);
                        users.RemoveAll(u => u.Id == id);
                        responseText = "Користувача видалено";
                    }
                }


                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/login")
                {
                    var body = new System.IO.StreamReader(request.InputStream).ReadToEnd();
                    var loginData = JsonSerializer.Deserialize<User>(body);

                    var user = users.FirstOrDefault(u => u.Email == loginData.Email && u.Password == loginData.Password);

                    if (user != null)
                    {
                        var token = GenerateJwtToken(user);
                        responseText = JsonSerializer.Serialize(new { Token = token });
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        responseText = "Невірна пошта або пароль";
                    }
                }

                else if (request.HttpMethod == "PATCH" && request.Url.AbsolutePath.StartsWith("/users/"))
                {
                    var userPrincipal = GetUserFromRequest(request);
                    if (userPrincipal == null)
                    {
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        responseText = "Неавторизовано";
                    }
                    else
                    {
                        int id = GetUserIdFromUrl(request.Url.AbsolutePath);
                        var user = users.Find(u => u.Id == id);

                        if (user == null)
                        {
                            response.StatusCode = (int)HttpStatusCode.NotFound;
                            responseText = "Користувача не знайдено";
                        }
                        else if (userPrincipal.IsInRole("Admin") || userPrincipal.Identity.Name == user.Email)  // 🔹 Адмін або власник акаунта
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
                                responseText = "Невірний формат даних";
                            }
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            responseText = "Заборонено: ви можете змінити інформацію тільки про себе";
                        }
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
        private ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("SecretKey_For_Admin_User_Autorization"); // Використовуємо правильний секретний ключ

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return principal;
            }
            catch
            {
                return null;  // Якщо токен недійсний, повертаємо null
            }
        }

        private ClaimsPrincipal GetUserFromRequest(HttpListenerRequest request)
        {
            if (!request.Headers.AllKeys.Contains("Authorization")) return null;

            var authHeader = request.Headers["Authorization"];
            if (!authHeader.StartsWith("Bearer ")) return null;

            var token = authHeader.Substring("Bearer ".Length).Trim();
            return ValidateToken(token);
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("SecretKey_For_Admin_User_Autorization"); // Використовуємо той самий ключ

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        }),
                Expires = DateTime.UtcNow.AddHours(1),  // Токен дійсний 1 годину
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

    }
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }      
    }

}

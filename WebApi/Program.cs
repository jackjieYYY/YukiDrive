using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YukiDrive.Services;
using YukiDrive.Models;
using YukiDrive.Contexts;
using Microsoft.AspNetCore;
using NLog.Web;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace YukiDrive
{
    public class Program
    {
        public static void Main(string[] args)
        {
            System.Console.WriteLine("开始启动程序...");
            //首次启动初始化
            Init();
            //忘记密码
            if (args.SingleOrDefault(str => str.Contains("newPassword:")) != null)
            {
                string pw = args.Single(str => str.Contains("newPassword:"));
                ChangePassword(pw);
            }
            //初始化 Logger
            var logger = NLog.Web.NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
            try
            {
                logger.Debug("init main");
                CreateHostBuilder().Build().Run();
            }
            catch (Exception exception)
            {
                //NLog: catch setup errors
                logger.Error(exception, "Stopped program because of exception");
                throw;
            }
            finally
            {
                // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
                NLog.LogManager.Shutdown();
            }
            System.Console.WriteLine("程序已关闭");
        }
        /// <summary>
        /// 创建主机
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IWebHostBuilder CreateHostBuilder()
        {
            return WebHost.CreateDefaultBuilder()
            .UseUrls(Configuration.Urls) //使用自定义url
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            })
            .UseNLog()
            .ConfigureKestrel(options =>
            {
                options.ConfigureHttpsDefaults(options =>
                {
                    try
                    {
                        if (Configuration.HttpsCertificate.Enable)
                        {
                            options.ServerCertificate = new X509Certificate2(Configuration.HttpsCertificate.FilePath, Configuration.HttpsCertificate.Password);
                        }
                    }
                    catch (System.Exception e)
                    {
                        System.Console.WriteLine("https 证书配置错误");
                        System.Console.WriteLine(e.Message);
                    }

                });
            })
            .UseStartup<Startup>();
        }

        public static void Init()
        {
            //初始化
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "YukiDrive.db")))
            {
                File.Copy("YukiDrive.template.db", "YukiDrive.db");
                System.Console.WriteLine("数据库创建成功");
            }
            using (SettingService settingService = new SettingService(new SettingContext()))
            {
                if (settingService.Get("IsInit") != "true")
                {
                    using (UserService userService = new UserService(new UserContext()))
                    {
                        User adminUser = new User()
                        {
                            Username = YukiDrive.Configuration.AdminName
                        };
                        userService.Create(adminUser, YukiDrive.Configuration.AdminPassword);
                    }
                    settingService.Set("IsInit", "true").Wait();
                    System.Console.WriteLine("数据初始化成功");
                    System.Console.WriteLine($"管理员名称：{Configuration.AdminName}");
                    System.Console.WriteLine($"管理员密码：{Configuration.AdminPassword}");
                    System.Console.WriteLine($"请登录 {Configuration.BaseUri}/#/login 进行身份及其他配置");
                }
            }
        }

        public static void ChangePassword(string newPassword)
        {
            using (UserService userService = new UserService(new UserContext()))
            {
                userService.Update(userService.GetByUsername(Configuration.AdminName), YukiDrive.Configuration.AdminPassword);
            }
            System.Console.WriteLine("密码更新成功");
        }
    }
}

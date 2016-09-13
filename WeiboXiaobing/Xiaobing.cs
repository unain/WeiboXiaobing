using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace WeiboXiaobing
{
    public class Xiaobing
    {
        HttpClient client;
        CookieContainer cookie;
        HttpClientHandler handle;
        Uri baseAddress;
        Random random;

        string Server;
        string Channel;

        string Jsonp;
        string ClientId;
        int Id;
        public BlockingCollection<string> replys = new BlockingCollection<string>(100);
        
        public void AddCookieFromJson(string filename)
        {
            var cookieCollection = new CookieCollection();
            var json = File.ReadAllText(filename);
            dynamic cookies = JArray.Parse(json);
            foreach (var c in cookies)
            {
                string value = (string)(c.value);
                if (value.Contains(","))
                {
                    value = value.Replace(",", string.Empty);
                    cookieCollection.Add(new Cookie((string)(c.name), value,
                                                                    (string)(c.path), (string)(c.domain)));
                }
                else
                    cookieCollection.Add(new Cookie((string)(c.name), (string)(c.value),
                                                    (string)(c.path), (string)(c.domain)));
            }
            cookie.Add(cookieCollection);
        }
        public Xiaobing()
        {
            random = new Random();
            cookie = new CookieContainer();

            //add cookie
            AddCookieFromJson("cookie.json");
            handle = new HttpClientHandler();
            handle.CookieContainer = cookie;
            client = new HttpClient(handle, false);
            baseAddress = new Uri("http://10.80.web1.im.weibo.com");
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.57 Safari/537.36");
            client.DefaultRequestHeaders.Referrer =
                new Uri("http://api.weibo.com/chat/");
            Wait();
        }

        internal string Send(String data1)
        {
            //Console.WriteLine("Send Running");
            var time = (long)DateTime.UtcNow
               .Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
               .TotalMilliseconds;
            string data = @"text=" + data1 + "&uid=5175429989";
            var content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
            var result = client.PostAsync("http://api.weibo.com/chat/2/direct_messages/new.json?source=209678993", content).Result;
            result.EnsureSuccessStatusCode();
            //Console.WriteLine("Send Complete");
            var x = result.Content.ReadAsStringAsync().Result;
           // return result.Content.;
            return x;
        }
        public void Wait()
        {
            Id = 3;
            string result1 = OrinaryGet("http://nas.im.api.weibo.com/im/webim.jsp?returntype=json&v=1.1&source=209678993&callback=angular.callbacks._2").Result;

            result1 = ParseJsonp(result1, @"angular\.callbacks\._2\((.*)\);\}catch");
            dynamic x = JObject.Parse(result1);
             Server = x.server;
             Channel = x.channel;
            string ids = HandShake(Server).Result;
             Jsonp = ParseJsonp(ids, @"try\{(.*)\(\[\{");
             ClientId = ParseJsonp(ids, @"clientId"":""(.*)"",""supportedConnectionTypes");
            result1 = Subscript(Server, Jsonp, Channel, ClientId).Result;
            Task.Run(() =>
             {
                 while (true)
                 {
                     try
                     {
                         string result = WaitAsync().Result;
                         replys.Add(result);
                         
                         if (result.Contains(@""":{""type"":""msg"""))
                         {
                             string mid = ParseJsonp(result, @"dmid"":""(.*)"",""dm_type");
                             SetUnRead(mid);
                         }
                         if (result.Contains(@""":{""type"":""unreader"""))
                         {
                             string mid = ParseJsonp(result, @"lastmid"":(.*),""dm_isRemind");
                             SetUnRead(mid);
                         }
                     }
                     catch (Exception e)
                     {
                         Console.WriteLine(e.Message);
                        
                     }
                 }
             });
            Thread.Sleep(2000);

        }

        private void SetUnRead(string mid)
        {
           
            var time = (long)DateTime.UtcNow
               .Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
               .TotalMilliseconds;
            string data = @"is_include_group=0&type=2&uid=5175429989";
            var content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
            var result = client.PostAsync("http://api.weibo.com/chat/2/direct_messages/set_unread_count.json?mid="+mid+"&source=209678993", content).Result;
            result.EnsureSuccessStatusCode();
           
        }
        internal async Task<string> WaitAsync()
        {
            var time = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).Ticks / 1000;
            //client.Timeout = 1000;
            var response = await client.GetAsync(
                Server + "im/connect?jsonp=" + Jsonp + "&message=%5B%7B%22channel%22%3A%22%2Fmeta%2Fconnect%22%2C%22connectionType%22%3A%22callback-polling%22%2C%22id%22%3A%22" + Id
                + "%22%2C%22clientId%22%3A%22" + ClientId + "%22%7D%5D"
                + "&_=" + time);
            response.EnsureSuccessStatusCode();
            string urlContents = await response.Content.ReadAsStringAsync();
            Id++;
            return urlContents;
        }
        private async Task<string> Subscript(string server, string jsonp, string channel, string clientId)
        {
            var time = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).Ticks / 1000;
            var response = await client.GetAsync(
                server + "im/?jsonp=" + jsonp
                + "&message=%5B%7B%22channel%22%3A%22%2Fmeta%2Fsubscribe%22%2C%22subscription%22%3A%22" + channel
                + "%22%2C%22id%22%"+Id+"A%223%22%2C%22clientId%22%3A%22" + clientId + "%22%7D%5D&_=" + time);
            response.EnsureSuccessStatusCode();
            string urlContents = await response.Content.ReadAsStringAsync();
            return urlContents;
            
        }
        private void Subscript2(string msg, string mid)
        {
            string uid = "5175429989";
            var time = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).Ticks / 1000;
            var response = client.GetAsync(
                Server + "im/?jsonp=" + Jsonp
                + "&message=%5B%7B%22channel%22%3A%22%2Fim%2Freq%22%2C%22data%22%3A%7B%22cmd%22%3A%22synchroniz%22%2C%22seq%22%3A%22fixed-send%22%2C%22syncData%22%3A%22%7B%5C%22uid%5C%22%3A%5C%22"
                +uid +"%5C%22%2C%5C%22msg%5C%22%3A%5C%22"
                +msg+"%5C%22%2C%5C%22long%5C%22%3Afalse%2C%5C%22mid%5C%22%3A"
                +mid+"%2C%5C%22time%5C%22%3A1473698567512%7D%22%7D%2C%22id%22%3A%22"
                +Id+"%22%2C%22clientId%22%3A%22"
                +ClientId+"%22%7D%5D&_=" + time).Result;
            response.EnsureSuccessStatusCode();
           
            Id++;
        }

        private string ParseJsonp(string ids, string match)
        {
            string id = string.Empty;
            Regex regex = new Regex(match);
            MatchCollection m = regex.Matches(ids);
            if (m.Count > 0)
            {
                id = m[m.Count - 1].Groups[1].Value;
            }
            return id;
        }

        private async Task<string> OrinaryGet(string url)
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string urlContents = await response.Content.ReadAsStringAsync();
            return urlContents;
        }

        private async Task<string> HandShake(string server)
        {
            var time = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).Ticks / 1000;
            var response = await client.GetAsync(
                server + "im/handshake?jsonp=jQuery21402673409309094459_" + time
                + "&message=%5B%7B%22version%22%3A%221.0%22%2C%22minimumVersion%22%3A%221.0%22%2C%22channel%22%3A%22%2Fmeta%2Fhandshake%22%2C%22supportedConnectionTypes%22%3A%5B%22callback-polling%22%5D%2C%22advice%22%3A%7B%22timeout%22%3A60000%2C%22interval%22%3A0%7D%2C%22id%22%3A%222%22%7D%5D&_=" + time);
            response.EnsureSuccessStatusCode();
            string urlContents = await response.Content.ReadAsStringAsync();
            return urlContents;
        }

        public string Say(string message)
        {
            while (replys.Count > 0)
            {
                String item;
                replys.TryTake(out item);
            }
          
            var resultx = this.Send(message);
            dynamic ssss = JObject.Parse(resultx);
            this.Subscript2(message, (string)(ssss.mid));
            
            while (true)
            {

                String result = string.Empty;         
                if (replys.TryTake(out result))
                {
                    if (result.Contains(@""":{""type"":""msg""") )
                    {
                        return ParseJsonp(result, @"content"":""(.*)"",""time");
                    }
                    
                }
            }

        }
        static void Main()
        {
            var s = new Xiaobing();
            //Thread.Sleep(1000 * 30);
            Console.WriteLine(s.Say("hi"));
            Console.WriteLine(s.Say("你好吗"));
            Console.WriteLine(s.Say("睡了没"));
            Console.WriteLine(s.Say("在干嘛呢"));
            Console.WriteLine(s.Say("吃饭没"));
        }
    }
}

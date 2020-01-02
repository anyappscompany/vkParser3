using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Threading;
using System.Web;

using Google.GData.Extensions;
using Google.GData.YouTube;
using Google.GData.Extensions.MediaRss;
using Google.YouTube;
using Google.GData.Client;

namespace vkParser3
    
{
    class Program
    {
        static string remixsid;  //Id сессии
        static public string lastCookies; //Куки
        struct vidosi
        {
            public string url;
            public string title;
            public string desc;
        }
        static Queue<vidosi> URLs = new Queue<vidosi>();
        //список скачанных страниц
        static List<string> HTMLs = new List<string>();
        //локер для очереди адресов
        static object URLlocker = new object();
        //локер для списка скачанных страниц
        static object HTMLlocker = new object();
        //очередь ошибок
        static Queue<Exception> exceptions = new Queue<Exception>();

        static void Main(string[] args)
        {
            string post = "email=" + "max007@mail.ru" + "&pass=" + "Qwertyui1" + "&q=1&act=login&q=1&al_frame=1&expire=&captcha_sid=&captcha_key=&from_host=vk.com&from_protocol=http&ip_h=4e78766a2890ac1115&quick_expire=1";
            string html = GetHtml(@"https://vk.com/", "");
            html = GetHtml(@"https://login.vk.com/?act=login", post);
            Console.WriteLine(html);

            Regex rex4 = new Regex(@"parent\.onLoginDone\(\'(.*?)\'\)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            Match matc4 = rex4.Match(html);
            string userid = matc4.Groups[1].ToString().Replace("/id", "");
            Console.WriteLine("Id: " + userid);
            //html = GetHtml(@"https://vk.com/id" + userid, "");

            while (true)
            {
                URLs.Clear();
                html = GetHtml(@"http://vk.com/feed?q=has%3Avideo&section=search", "");
                File.WriteAllText(@"WriteText.txt", html);
                // тайтлы <b class="fl_l video"></b><span class="a post_video_title">(.*?)</span> <span class="post_video_duration">
                // видосы href="/video(.*?)\?list   (убрать - и дубликаты из массива)
                int status = Testlogin(html);
                Console.WriteLine("Результат: " + status);
                //(.*?)

                Regex postReg = new Regex(@"return showVideo\(\'(?<video>.*?)\', \'(?<list>.*?)\', \{autoplay", RegexOptions.Singleline);
                MatchCollection matches = postReg.Matches(html);
                Console.WriteLine(matches.Count);
                if (matches.Count > 0)
                {
                    foreach (Match mat in matches)
                    {
                        string link = "http://vk.com/video" + mat.Groups["video"].Value + "?list=" + mat.Groups["list"].Value;
                        Console.WriteLine(link);
                        using (System.IO.StreamWriter file = new StreamWriter("file.txt", true, Encoding.Default))
                        {
                            file.WriteLine(link);
                        }
                        string HTML = GetHtml(link, "");

                        Regex mp4Reg = new Regex(@"http:\\\\\\\/\\\\\\\/(?<cs>.*?).vk.me\\\\\\\/(?<uu>.*?)\\\\\\\/videos\\\\\\\/(?<fb>.*?).mp4", RegexOptions.Singleline);
                        MatchCollection mp4Matches = mp4Reg.Matches(HTML);
                        if (mp4Matches.Count > 0)
                        {
                            //получение ссылки на видео
                            string urlvideo = "";
                            foreach (Match mata in mp4Matches)
                            {
                                //urlvideo = "http" + mata.Groups["urlVideo"].Value.Replace(@"\\\", "") + ".mp4";
                                urlvideo = "http://" + mata.Groups["cs"].Value + ".vk.me/" + mata.Groups["uu"].Value + "/videos/" + mata.Groups["fb"].Value + ".mp4";
                                using (System.IO.StreamWriter file = new StreamWriter("videos.txt", true, Encoding.Default))
                                {
                                    file.WriteLine(urlvideo);
                                }
                            }
                            Console.WriteLine("");
                            //получение тайтла видео
                            string title = "";
                            Regex titleReg = new Regex("<title>(?<titleVideo>.*?)</title>", RegexOptions.Singleline);
                            MatchCollection titleMatches = titleReg.Matches(HTML);
                            if (titleMatches.Count > 0)
                            {

                                foreach (Match mata2 in titleMatches)
                                {
                                    title = HttpUtility.HtmlDecode(mata2.Groups["titleVideo"].Value);
                                }
                            }
                            Console.WriteLine("Title: " + title);

                            //получение тайтла видео
                            string descr = "";
                            Regex descrReg = new Regex("<meta name=\"description\" content=\"(?<descrVideo>.*?)\" />", RegexOptions.Singleline);
                            MatchCollection descrMatches = descrReg.Matches(HTML);
                            if (descrMatches.Count > 0)
                            {

                                foreach (Match mata3 in descrMatches)
                                {
                                    descr = HttpUtility.HtmlDecode(mata3.Groups["descrVideo"].Value);
                                }
                            }

                            Console.WriteLine("Description: " + descr);
                            Console.WriteLine("UrlVideo: " + urlvideo);
                            Console.WriteLine("");

                            vidosi vds = new vidosi();
                            vds.title = title.Replace("\\", "");
                            vds.desc = descr.Replace("\\", "");
                            vds.url = urlvideo;

                            URLs.Enqueue(vds);

                            //Download(urlvideo, AppDomain.CurrentDomain.BaseDirectory + "video\\" + title + ".mp4");
                        }


                        Console.WriteLine(mp4Matches.Count + " m");
                        Thread.Sleep(1000);
                    }
                }

                //создаем массив хендлеров, для контроля завершения потоков
                ManualResetEvent[] handles = new ManualResetEvent[10];
                //создаем и запускаем 3 потока
                for (int i = 0; i < 10; i++)
                {
                    handles[i] = new ManualResetEvent(false);
                    (new Thread(new ParameterizedThreadStart(Download))).Start(handles[i]);
                }
                //ожидаем, пока все потоки отработают
                WaitHandle.WaitAll(handles);

                //проверяем ошибки, если были - выводим
                foreach (Exception ex in exceptions)
                    Console.WriteLine(ex.Message);

                Console.WriteLine("Download completed");
                //Console.ReadLine();
            }

            Console.ReadKey();

        }
        static public string GetHtml(string url, string postData) //Возвращает содержимое поданной страницы
        {
            string HTML = "";

            Regex rex1 = new Regex("remixsid=(.*?);", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            if (url == "0") return "0"; //Проверка на ошибку
            HttpWebRequest myHttpWebRequest = (HttpWebRequest)HttpWebRequest.Create(url);
            //myHttpWebRequest.Proxy = new WebProxy("127.0.0.1", 8888); //В перспективе можно использовать прокси
            if (!String.IsNullOrEmpty(postData)) myHttpWebRequest.Method = "POST";
            myHttpWebRequest.Referer = "https://vk.com";
            myHttpWebRequest.UserAgent = "Mozila/14.0 (compatible; MSIE 6.0;Windows NT 5.1; SV1; MyIE2;";
            myHttpWebRequest.Accept = "image/gif, image/x-xbitmap, image/jpeg,image/pjpeg, application/x-shockwave-flash,application/vnd.ms-excel,application/vnd.ms-powerpoint,application/msword";
            myHttpWebRequest.Headers.Add("Accept-Language", "ru");
            myHttpWebRequest.ContentType = "application/x-www-form-urlencoded";
            myHttpWebRequest.KeepAlive = false;

            // передаем Сookie, полученные в предыдущем запросе
            if (!String.IsNullOrEmpty(remixsid))
            {
                lastCookies = "remixchk=5;remixsid=" + remixsid;
            }
            if (!String.IsNullOrEmpty(lastCookies))
            {
                myHttpWebRequest.Headers.Add(System.Net.HttpRequestHeader.Cookie, lastCookies);
            }
            // ставим False, чтобы при получении кода 302, не делать 
            // автоматического перенаправления
            myHttpWebRequest.AllowAutoRedirect = false;

            // передаем параметры
            string sQueryString = postData;
            byte[] ByteArr = System.Text.Encoding.GetEncoding(1251).GetBytes(sQueryString); //Вконтакте использует кирилическую кодировку
            try
            {
                if (!String.IsNullOrEmpty(postData))
                {
                    myHttpWebRequest.ContentLength = ByteArr.Length;
                    myHttpWebRequest.GetRequestStream().Write(ByteArr, 0, ByteArr.Length);
                };

                // делаем запрос
                HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                StreamReader myStreamReader;

                //Сохраняем Cookie 
                lastCookies = String.IsNullOrEmpty(myHttpWebResponse.Headers["Set-Cookie"]) ? "" : myHttpWebResponse.Headers["Set-Cookie"];
                Match matc1 = rex1.Match(lastCookies);

                //Если есть имя сессии, то подменяем Cookie 
                if (matc1.Groups.Count == 2) { remixsid = matc1.Groups[1].ToString(); lastCookies = "remixchk=5;remixsid=" + remixsid; }
                if (myHttpWebResponse.Headers["Content-Type"].IndexOf("windows-1251") > 0)
                {
                    myStreamReader = new StreamReader(myHttpWebResponse.GetResponseStream(), Encoding.GetEncoding("windows-1251"));
                }
                else
                {
                    myStreamReader = new StreamReader(myHttpWebResponse.GetResponseStream(), Encoding.UTF8);
                }
                HTML = myStreamReader.ReadToEnd();
                if (HTML == "") //Проверяем на редирект
                {
                    HTML = GetHtml(myHttpWebResponse.Headers["Location"].ToString(), "");

                }
            }
            catch (Exception err)
            {
                //Ошибка в чтении страницы
                return "0";
            }
            return HTML;
        }
        static private int Testlogin(string html)
        {
            int status = 0;
            if (html.IndexOf("login?act=blocked") > 0) { status = 2; return 2; }
            if (html.IndexOf("onLoginFailed") > 0) { status = 3; return 3; }
            Regex rex1 = new Regex("href=\"edit\"", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            //Match matc1 = rex1.Match(html);
            //if (matc1.Groups[0].Length == 0) { status = 4; return 4; }
            //status = 1;
            return 1;
        }
        public static int Downloadfile(String remoteFilename,
                               String localFilename)
        {
                 HttpWebRequest r0 = (HttpWebRequest)HttpWebRequest.Create(remoteFilename);
                 r0.Method = "GET";
                 HttpWebResponse res = (HttpWebResponse)r0.GetResponse();
                 string Text = res.ContentLength.ToString();
                 if (Convert.ToInt32(Text) > 10000000) //250mb = 250000000
                 {
                     return 0;
                 }
                 Console.WriteLine("FileSize: " + Text);
             
            // Function will return the number of bytes processed
            // to the caller. Initialize to 0 here.
            int bytesProcessed = 0;

            // Assign values to these objects here so that they can
            // be referenced in the finally block
            Stream remoteStream = null;
            Stream localStream = null;
            WebResponse response = null;

            // Use a try/catch/finally block as both the WebRequest and Stream
            // classes throw exceptions upon error

            // Create a request for the specified remote file name
            WebRequest request = WebRequest.Create(remoteFilename);
            if (request != null)
            {
                // Send the request to the server and retrieve the
                // WebResponse object 
                response = request.GetResponse();
                if (response != null)
                {
                    // Once the WebResponse object has been retrieved,
                    // get the stream object associated with the response's data
                    remoteStream = response.GetResponseStream();

                    // Create the local file
                    localStream = File.Create(localFilename);

                    // Allocate a 1k buffer
                    byte[] buffer = new byte[1024];
                    int bytesRead;

                    // Simple do/while loop to read from stream until
                    // no bytes are returned
                    do
                    {
                        // Read data (up to 1k) from the stream
                        bytesRead = remoteStream.Read(buffer, 0, buffer.Length);

                        // Write the data to the local file
                        localStream.Write(buffer, 0, bytesRead);

                        // Increment total bytes processed
                        bytesProcessed += bytesRead;
                    } while (bytesRead > 0);
                }

            }

            // Return total bytes processed to caller.
            return bytesProcessed;
        }
        public static void Download(object handle)
        {
            //будем крутить цикл, пока не закончатся ULR в очереди
            while (true)
                try
                {
                    string URL;
                    string titt;
                    string desss;
                    //блокируем очередь URL и достаем оттуда один адрес
                    lock (URLlocker)
                    {
                        if (URLs.Count == 0)
                            break;//адресов больше нет, выходим из метода, завершаем поток
                        else
                        {
                            vidosi tmpvid = URLs.Dequeue();
                            URL = tmpvid.url;
                            titt = tmpvid.title.Replace("\\", "");
                            desss = titt + " " + tmpvid.desc.Replace("\\", "");
                        }
                    }
                    Console.WriteLine(URL + " - start downloading ...");
                    //скачиваем страницу
                    if (titt == "No name" || titt == "no name")
                    {
                        continue;
                    }
                    if (URL.IndexOf(".flv") != -1)
                    {
                        continue;
                    }
                    string filename = titt.Replace(@"\", "").Replace(@"/", "").Replace(@":", "").Replace(@"*", "").Replace(@"?", "").Replace("\"", "").Replace(@"<", "").Replace(@">", "").Replace(@"|", "").Replace(@".", "");
                    int bytesss = Downloadfile(URL, AppDomain.CurrentDomain.BaseDirectory + "video\\" + filename + ".mp4");
                    lock (HTMLlocker)
                    {
                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "video\\upload.csv", true, Encoding.UTF8))
                        {
                            if (desss == "") { desss = titt; }
                            //file.WriteLine(titt + "05452" + desss + "05452" + titt.Replace(@"\", " ").Replace(@"/", " ") + ".mp4");
                            file.WriteLine(titt + "05452" + desss + "05452" + titt + "05452" + "People" + "05452" + "TRUE" + "05452" + AppDomain.CurrentDomain.BaseDirectory + "video\\" + titt.Replace(@"\", "").Replace(@"/", "").Replace(@":", "").Replace(@"*", "").Replace(@"?", "").Replace("\"", "").Replace(@"<", "").Replace(@">", "").Replace(@"|", "").Replace(@".", "") + ".mp4");
                        }
                    }
                    Console.WriteLine(URL + " - downloaded (" + bytesss + " bytes)");
                }
                catch (ThreadAbortException)
                {
                    //это исключение возникает если главный поток хочет завершить приложение
                    //просто выходим из цикла, и завершаем выполнение
                    break;
                }
                catch (Exception ex)
                {
                    //в процессе работы возникло исключение
                    //заносим ошибку в очередь ошибок, предварительно залочив ее
                    lock (exceptions)
                        exceptions.Enqueue(ex);
                    //берем следующий URL
                    continue;
                }
            //устанавливаем флажок хендла, что бы сообщить главному потоку о том, что мы отработали
            ((ManualResetEvent)handle).Set();
        }
    }
}

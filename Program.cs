using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ScraperC_
{
    internal class Program
    {
        public class RedditPost
        {
            // Egenskaper för titel, poäng, subreddit, url och post body text
            public string Title { get; set; }
            public int Score { get; set; }
            public string Subreddit { get; set; }
            public string Url { get; set; }
            public string ImageUrl { get; set; } // New property for image URL
            public string Body { get; set; } // New property for post body text
        }

        // Skapa en metod för att ladda ner en reddit-sida och returnera en lista av reddit-inlägg
        public static List<RedditPost> DownloadRedditPage(string subreddit, string after = "")
        {
            // Skapa en tom lista av reddit-inlägg
            List<RedditPost> posts = new List<RedditPost>();

            // Skapa en webbklient-instans
            WebClient client = new WebClient();

            // Lägg till en användaragent-rubrik för att undvika 429-fel
            client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

            // Skapa en url för att begära reddit-sidan i json-format
            // Om after-parametern är angiven, lägg till den till url för att begära nästa sida
            string url = "https://www.reddit.com/r/" + subreddit + "/new" + ".json";
            if (after != "")
            {
                url += "?after=" + after;
            }

            // Gör en GET-förfrågan till url och få svaret
            string response = client.DownloadString(url);

            // Analysera svaret som ett dynamiskt objekt med Newtonsoft.Json
            dynamic data = JsonConvert.DeserializeObject(response);

            // Loopa genom varje barn i data-objektet
            foreach (var child in data.data.children)
            {
                // Skapa ett nytt reddit-inlägg med egenskaperna från barnet
                RedditPost post = new RedditPost();
                post.Title = child.data.title;
                post.Score = child.data.score;
                post.Subreddit = child.data.subreddit;
                post.Url = child.data.url;

                // Lägg till image URL om det finns en bild i inlägget
                if (child.data.thumbnail != "self" && child.data.thumbnail != "default")
                {
                    post.ImageUrl = child.data.url_overridden_by_dest; // Use the actual image URL
                }

                // Lägg till post body text om det finns
                if (child.data.selftext != null) // Check for null explicitly
                {
                    post.Body = child.data.selftext;
                }

                // Lägg till reddit-inlägget till listan
                posts.Add(post);
            }

            // Returnera listan av reddit-inlägg
            return posts;
        }

        // Skapa en metod för att spara ett reddit-inlägg till en textfil
        // ...

        public static void SaveRedditPost(RedditPost post, string rootFolder, bool saveAsJson)
        {
            // Skapa en mapp för varje post baserat på dess titel och subreddit
            string postFolder = Path.Combine(rootFolder, $"{post.Title} [{post.Subreddit}]");
            Directory.CreateDirectory(postFolder);

            if (saveAsJson)
            {
                // Save the post as JSON data
                string jsonFilePath = Path.Combine(postFolder, $"{post.Title} ({post.Score}).json");
                File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(post, Formatting.Indented));
            }
            else
            {
                // Save the post as plain text
                string textFilePath = Path.Combine(postFolder, $"{post.Title} ({post.Score}).txt");

                // Skapa en ström-skrivare för att skriva till textfilen
                using (StreamWriter writer = new StreamWriter(textFilePath))
                {
                    // Skriv titeln, poängen, subredditet, url och post body text till textfilen
                    writer.WriteLine("Title: " + post.Title);
                    writer.WriteLine("Score: " + post.Score);
                    writer.WriteLine("Subreddit: " + post.Subreddit);
                    writer.WriteLine("Url: " + post.Url);
                    writer.WriteLine("Body: " + WebUtility.HtmlDecode(post.Body)); // Decode HTML-encoded text

                    // Lägg till image URL i textfilen om det finns en bild
                    if (!string.IsNullOrEmpty(post.ImageUrl))
                    {
                        writer.WriteLine("ImageUrl: " + post.ImageUrl);

                        // Skapa en filväg för bildfilen
                        string imageFilePath = Path.Combine(postFolder, $"{post.Title}_image.jpg");

                        // Ladda ner och spara bilden
                        DownloadAndSaveImage(post.ImageUrl, imageFilePath);
                    }
                }
            }
        }

        // ...



        // Skapa en metod för att ladda ner och spara reddit-inlägg med en array av nyckelord
        public static void DownloadAndSaveRedditPosts(string subreddit, string[] keywords, string folder)
        {
            // Skapa en variabel för att lagra after-parametern för nästa sida
            string after = "";

            // Loopa tills det inte finns någon nästa sida
            while (true)
            {
                // Ladda ner reddit-sidan med den angivna subredditet och after-parametern
                List<RedditPost> posts = DownloadRedditPage(subreddit, after);

                // Loopa genom varje reddit-inlägg i listan
                foreach (RedditPost post in posts)
                {
                    // Loopa genom varje nyckelord i arrayen
                    foreach (string keyword in keywords)
                    {
                        // Kontrollera om titeln innehåller nyckelordet, antingen delvis eller helt
                        if (post.Title.ToLower().Contains(keyword.ToLower()))
                        {
                            // Spara reddit-inlägget till den angivna mappen
                            SaveRedditPost(post, folder, saveAsJson: true); // Configuration variable, whether to save as JSON or not.

                            // Bryt ut ur nyckelords-loopen
                            break;
                        }
                    }
                }

                // Hämta after-parametern från data-objektet för nästa sida
                ////after = data.data.after; // Uncomment this line

                // Kontrollera om after-parametern är null, vilket betyder att det inte finns någon nästa sida
                if (after == null)
                {
                    // Bryt ut ur sid-loopen
                    break;
                }
            }
        }

        // Skapa en metod för att ladda ner och spara en bild från en URL
        public static void DownloadAndSaveImage(string imageUrl, string filePath)
        {
            WebClient client = new WebClient();
            client.DownloadFile(imageUrl, filePath);
        }

        static void Main(string[] args)
        {
            // Testa metoderna med ett exempel
            // Ange en array av subreddits att övervaka
            string[] subreddits = { "gnuscreen", "irssi", "truenas" };

            // Skapa en dictionary för att lagra nyckelord per subreddit
            Dictionary<string, string[]> subredditKeywords = new Dictionary<string, string[]>
            {
                { "gnuscreen", new string[] { "release"} },
                { "irssi", new string[] { "release", "Ignore" } },
                { "truenas", new string[] { "release", "zfs" } }
            };

            // Ange en mapp att spara inläggen i
            string folder = "E:\\downloaded\\Scraping\\Reddit";

            // Loopa genom varje subreddit och övervaka parallellt
            List<Task> tasks = new List<Task>();
            foreach (string subreddit in subreddits)
            {
                // Anropa metoden för att ladda ner och spara reddit-inlägg för varje subreddit
                tasks.Add(Task.Run(() => DownloadAndSaveRedditPosts(subreddit, subredditKeywords[subreddit], folder)));
            }

            // Vänta på att alla uppgifter ska slutföras
            Task.WaitAll(tasks.ToArray());
        }
    }
}

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
            public string Id { get; set; }
            public string Title { get; set; }
            public int Score { get; set; }
            public string Subreddit { get; set; }
            public string Url { get; set; }
            public string ImageUrl { get; set; } 
            public string Body { get; set; } 
        }


        public class SubredditKeywordsData
        {
            public Dictionary<string, string[]> SubredditKeywords { get; set; }
        }

        private static SubredditKeywordsData LoadSubredditKeywordsFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<SubredditKeywordsData>(json);
            }
            else
            {
                return new SubredditKeywordsData { SubredditKeywords = new Dictionary<string, string[]>() };
            }
        }

        private static void SaveSubredditKeywordsToFile(string filePath, SubredditKeywordsData data)
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static List<RedditPost> DownloadRedditPage(string subreddit, string after = "")
        {
            List<RedditPost> posts = new List<RedditPost>();

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
            dynamic jsonData = JsonConvert.DeserializeObject(response);

            // Loopa genom varje barn i data-objektet
            foreach (var child in jsonData.data.children)
            {
                // Skapa ett nytt reddit-inlägg med egenskaperna från barnet
                RedditPost post = new RedditPost();
                post.Id = child.data.id; 
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


            // Hämta after-parametern från data-objektet för nästa sida
            string nextAfter = jsonData.data.after;

            // Uppdatera after-parametern
            after = string.IsNullOrEmpty(nextAfter) ? null : nextAfter;

            // Returnera listan av reddit-inlägg
            return posts;
        }

        public static void SaveRedditPost(RedditPost post, string rootFolder, bool saveAsJson)
        {
            string sanitizedTitle = new string(post.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            string postFolder = Path.Combine(rootFolder, $"{sanitizedTitle} [{post.Subreddit}]");

            Directory.CreateDirectory(postFolder);

            if (saveAsJson)
            {
                // Save the post as JSON data
                string jsonFilePath = Path.Combine(postFolder, $"{sanitizedTitle} ({post.Score}).json");

                File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(post, Formatting.Indented));
            }
            else
            {
                // Save the post as plain text
                string textFilePath = Path.Combine(postFolder, $"{post.Title} ({post.Score}).txt");

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

        // Skapa en metod för att ladda ner och spara reddit-inlägg med en array av nyckelord
        public static void DownloadAndSaveRedditPosts(string subreddit, string[] keywords, string folder)
        {
            string after = null;

            while (true)
            {
                bool matchFoundInSubreddit = false;

                // Download Reddit page with the specified subreddit and 'after' parameter
                List<RedditPost> posts = DownloadRedditPage(subreddit, after);

                foreach (RedditPost post in posts)
                {
                    foreach (string keyword in keywords)
                    {
                        Console.WriteLine($"Checking post '{post.Title}' for keyword '{keyword}'...");

                        if (post.Title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Console.WriteLine($"Match found in subreddit '{subreddit}' for keyword '{keyword}'!");
                            SaveRedditPost(post, folder, saveAsJson: true);

                            matchFoundInSubreddit = true;
                            break;
                        }
                    }
                }

                // Get the 'after' parameter from the 'next' field in the JSON response
                string nextAfter = posts.LastOrDefault()?.Id;

                if (string.IsNullOrEmpty(nextAfter))
                {
                    // No more pages for the current subreddit, break the loop
                    break;
                }

                // Update the 'after' parameter for the next iteration
                after = nextAfter;
            }
        }



        // Skapa en metod för att ladda ner och spara en bild från en URL
        public static void DownloadAndSaveImage(string imageUrl, string filePath)
        {
            WebClient client = new WebClient();
            client.DownloadFile(imageUrl, filePath);
        }

        // Helper method to get subreddit and keyword inputs from the user
        private static Dictionary<string, string[]> GetSubredditKeywordsFromInput(string filePath)
        {
            SubredditKeywordsData data = LoadSubredditKeywordsFromFile(filePath);
            Dictionary<string, string[]> subredditKeywords = data.SubredditKeywords;

            while (true)
            {
                Console.Write("Enter a subreddit to monitor (or 'start' to begin): ");
                string subreddit = Console.ReadLine().Trim();

                if (subreddit.ToLower() == "start")
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(subreddit))
                {
                    Console.WriteLine("Subreddit cannot be blank. Please enter a valid subreddit.");
                    continue;
                }

                Console.Write("Enter keywords for the subreddit (comma-separated): ");
                string keywordsInput = Console.ReadLine().Trim();

                if (string.IsNullOrWhiteSpace(keywordsInput))
                {
                    Console.WriteLine("Keywords cannot be blank. Please enter valid keywords.");
                    continue;
                }

                string[] keywords = keywordsInput.Split(',').Select(k => k.Trim()).ToArray();

                subredditKeywords[subreddit] = keywords;
            }

            data.SubredditKeywords = subredditKeywords;
            SaveSubredditKeywordsToFile(filePath, data);

            return subredditKeywords;
        }

        static void Main(string[] args)
        {
            // Specify the folder to save posts
            string folder = "E:\\downloaded\\Scraping\\Reddit";
            string filePath = Path.Combine(folder, "subreddits_and_keywords.json");

            // Load subreddit and keyword data
            Dictionary<string, string[]> subredditKeywords = GetSubredditKeywordsFromInput(filePath);

            // Check if there are any subreddits to monitor
            if (subredditKeywords.Count == 0)
            {
                Console.WriteLine("No subreddits provided. Exiting...");
                return;
            }

            // Main loop
            while (true)
            {
                Console.WriteLine("Choose an option:");
                Console.WriteLine("1. Traverse all available pages");
                Console.WriteLine("2. Monitor the latest page every X minutes");
                Console.WriteLine("3. Exit");

                string option = Console.ReadLine();

                switch (option)
                {
                    case "1":
                        // Traverse all available pages
                        foreach (var kvp in subredditKeywords)
                        {
                            string subreddit = kvp.Key;
                            string[] keywords = kvp.Value;

                            // Run the method to download and save Reddit posts for each subreddit
                            DownloadAndSaveRedditPosts(subreddit, keywords, folder);
                        }
                        break;

                    case "2":
                        // Monitor the latest page every X minutes
                        Console.Write("Enter the interval in minutes (e.g., 5): ");
                        if (int.TryParse(Console.ReadLine(), out int intervalMinutes) && intervalMinutes > 0)
                        {
                            // Inside the while (true) loop where you are checking subreddits
                            while (true)
                            {
                                Console.WriteLine("Starting a new iteration...");

                                foreach (var kvp in subredditKeywords)
                                {
                                    string subreddit = kvp.Key;
                                    string[] keywords = kvp.Value;

                                    Console.WriteLine($"Checking subreddit: {subreddit}");

                                    // Run the method to download and save Reddit posts for each subreddit
                                    DownloadAndSaveRedditPosts(subreddit, keywords, folder);
                                }

                                // Add a log statement to indicate the end of subreddit checks in this iteration
                                Console.WriteLine("Finished checking all subreddits for this iteration.");

                                // Wait for the specified interval
                                Console.WriteLine($"Waiting for {intervalMinutes} minutes...");
                                System.Threading.Thread.Sleep(intervalMinutes * 60 * 1000); // Convert minutes to milliseconds

                                // Add a log statement to indicate the end of the waiting period
                                Console.WriteLine("Finished waiting. Starting the next iteration...");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid interval. Exiting...");
                            return;
                        }

                    case "3":
                        // Exit the program
                        return;

                    default:
                        Console.WriteLine("Invalid option. Please enter a valid option.");
                        break;
                }
            }
        }

    }
}
// Valmöjlighet 2 fungerar bra (monitoring).
// Behöver arbeta på valmöjlighet 1 (att hämta alla posts från en subreddit), behöver antagligen skriva om till att använda API istället för webclient.
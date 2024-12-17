using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

        string connectionString = config.GetConnectionString("DefaultConnection");


        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--lang=en-US" }
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "en-US",
            TimezoneId = "America/New_York",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
        });

        await context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            { "Accept-Language", "en-US" }
        });

        var page = await context.NewPageAsync();

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            //string selectQuery = "SELECT LocationID, URL FROM tbl_GoogleMAPTBK WHERE OverallRating IS NULL OR ReviewCount IS NULL";
            string selectQuery = "SELECT LocationID, URL FROM tbl_GoogleMAPTBK";

            SqlCommand command = new SqlCommand(selectQuery, connection);

            using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection))
            {
                while (reader.Read())
                {
                    string locationId = reader["LocationID"].ToString();
                    string url = reader["URL"].ToString();

                    try
                    {
                        Console.WriteLine($"Scraping: {url}");

                        if (!url.Contains("hl="))
                        {
                            url += (url.Contains("?") ? "&" : "?") + "hl=en";
                        }

                        await page.GotoAsync(url);
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                        // Extract Review Count
                        //var reviewCountElement = page.Locator("button.HHrUdb span");
                        //string reviewCountText = await reviewCountElement.InnerTextAsync();
                        //int reviewCount = int.TryParse(reviewCountText.Split(' ')[0], out var parsedCount) ? parsedCount : 0;

                        //// Extract Overall Rating
                        //var overallRatingElement = page.Locator("css=.YTkVxc[role='img']");
                        //string ratingText = await overallRatingElement.GetAttributeAsync("aria-label");
                        ////float overallRating = float.TryParse(ratingText?.Split(' ')[0], out var parsedRating) ? parsedRating : 0.0f;
                        //double overallRating = double.TryParse(ratingText?.Split(' ')[0], out var parsedRating)
                        //? Math.Round(parsedRating, 2)
                        //: 0.0;

                        // Extract Review Count
                        //var reviewCountElement = page.Locator("button.HHrUdb span");
                        //string reviewCountText = await reviewCountElement?.InnerTextAsync() ?? "0"; // Default to "0" if null
                        //int reviewCount = int.TryParse(reviewCountText.Split(' ')[0], out var parsedCount) ? parsedCount : 0;

                        //// Extract Overall Rating
                        //var overallRatingElement = page.Locator("css=.YTkVxc[role='img']");
                        //string ratingText = await overallRatingElement?.GetAttributeAsync("aria-label") ?? "0.0"; // Default to "0.0" if null
                        //double overallRating = double.TryParse(ratingText.Split(' ')[0], out var parsedRating)
                        //    ? Math.Round(parsedRating, 2)
                        //    : 0.0;

                        // Extract Review Count
                        var reviewCountElement = page.Locator("button.HHrUdb span");
                        string reviewCountText = "0"; // Default value
                        if (await reviewCountElement.CountAsync() > 0) // Check if the locator exists
                        {
                            reviewCountText = await reviewCountElement.InnerTextAsync();
                        }
                        int reviewCount = int.TryParse(reviewCountText.Split(' ')[0], out var parsedCount) ? parsedCount : 0;

                        // Extract Overall Rating
                        var overallRatingElement = page.Locator("css=.YTkVxc[role='img']");
                        string ratingText = "0.0"; // Default value
                        if (await overallRatingElement.CountAsync() > 0) // Check if the locator exists
                        {
                            ratingText = await overallRatingElement.GetAttributeAsync("aria-label");
                        }
                        double overallRating = double.TryParse(ratingText?.Split(' ')[0], out var parsedRating)
                            ? Math.Round(parsedRating, 2)
                            : 0.0;



                        Console.WriteLine($"LocationID: {locationId}, Rating: {overallRating}, Reviews: {reviewCount}");

                        // Update the database
                        using (SqlConnection updateConnection = new SqlConnection(connectionString))
                        {
                            updateConnection.Open();
                            UpdateDatabase(updateConnection, locationId, overallRating, reviewCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error scraping {url}: {ex.Message}");
                    }
                }
            }
        }

        Console.WriteLine("Scraping completed!");
    }

    static void UpdateDatabase(SqlConnection connection, string locationId, double rating, int reviews)
    {
        string updateQuery = @"
            UPDATE tbl_GoogleMAPTBK
            SET OverallRating = @OverallRating, ReviewCount = @ReviewCount
            WHERE LocationID = @LocationID";

        using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection))
        {
            updateCommand.Parameters.AddWithValue("@OverallRating", rating);
            updateCommand.Parameters.AddWithValue("@ReviewCount", reviews);
            updateCommand.Parameters.AddWithValue("@LocationID", locationId);

            updateCommand.ExecuteNonQuery();
        }
    }
}

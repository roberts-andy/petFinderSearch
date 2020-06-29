using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using HtmlAgilityPack;
using System.Collections;
using System.Collections.Generic;
using RestSharp;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System.Linq;


namespace Company.Function
{
    public static class petFinderSearchHttp
    {
        [FunctionName("petFinderSearchHttp")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            [CosmosDB(
                databaseName: "%DbName%",
                collectionName: "%CollName%",
                ConnectionStringSetting = "CosmosDBConnection")] DocumentClient petsClient,

             [CosmosDB(
                databaseName: "%DbName%",
                collectionName: "%CollName%",
                ConnectionStringSetting = "CosmosDBConnection")]
                IAsyncCollector<dynamic> petsOut,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string token = GetToken(log);
            string res = ExecuteSearch(token, log);

            dynamic pets = JsonConvert.DeserializeObject(res);
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri("petfinder", "pets");
            List<dynamic> newPets = new List<dynamic>();
            foreach (dynamic pet in pets.animals)
            {


                log.LogInformation($"{pet.id}:{pet.name}");
                var query = petsClient.CreateDocumentQuery(collectionUri, $"select * from c where c.petId = '{pet.id}'").AsEnumerable();   
                if(query.FirstOrDefault() != null)
                {
                    log.LogInformation("Pet Found");
                }
                else
                {
                    log.LogInformation("Adding Pet");
                    dynamic newPet = new System.Dynamic.ExpandoObject();
                    newPet.id = pet.id.ToString();
                    newPet.petId = pet.id.ToString();
                    newPet.name = pet.name;
                    newPet.description = pet.description;
                    newPet.primaryBreed = pet.breeds.primary;
                    newPet.secondaryBreed = pet.breeds.secondary;
                    newPet.url = pet.url;

                    var photos = new List<dynamic>();
                    foreach(var photo in pet.photos)
                    {
                        photos.Add(photo.full);
                    }

                    newPet.photos = photos;

                    await petsOut.AddAsync(newPet);
                    newPets.Add(newPet);
                    // await petsOut.AddAsync(JsonConvert.SerializeObject(newPet).Replace("\"","'")    );

                }

            }

            return new OkObjectResult(newPets);
        }

        private static string GetToken(ILogger log)
        {
            log.LogInformation("Getting Token");
            string logonUri = Environment.GetEnvironmentVariable("logonUri");
            var client = new RestClient(logonUri);
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", "Basic TXdZUTRma1Y3QzlvMmh5R1Q5emlvdTBSZFJ0TUV4cWlBYUpXbVJSdWVVd2JTYkFWTDc6TzZsQldrNUxqbndBckRRV083WjZBSjRCWlhwTzluZTFiQ1Q3VG1aRA==");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "client_credentials");
            IRestResponse response = client.Execute(request);

            dynamic responseObj = JsonConvert.DeserializeObject(response.Content);
            //Console.WriteLine(response.Content);
            log.LogInformation("Token Generated");
            return responseObj.access_token;

        }

        private static string ExecuteSearch(string token, ILogger log)
        {
            log.LogInformation("Executing Search");
            string searchUri = Environment.GetEnvironmentVariable("searchUri");
            string searchQuery = Environment.GetEnvironmentVariable("searchQuery");
            log.LogInformation($"searchQuery = {searchUri}{searchQuery}");
            var client = new RestClient($"{searchUri}{searchQuery}");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", $"Bearer {token}");
            IRestResponse response = client.Execute(request);
            //Console.WriteLine(response.Content);
            log.LogInformation("Serach Complete");
            return response.Content;

        }
    }

}

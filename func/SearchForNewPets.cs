using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using RestSharp;


namespace PetFinderSearch
{
    public static class SearchForNewPets
    {
        [FunctionName("SearchForNewPets")]
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
            try
            {

                string dbName = System.Environment.GetEnvironmentVariable("DbName");
                string collName = System.Environment.GetEnvironmentVariable("CollName");

                log.LogInformation("C# HTTP trigger function processed a request.");

                //
                // Get my bearer token and execute search against petfinder api
                //
                string token = await GetToken(log);
                string searchResult = await ExecuteSearch(token, log);

                //
                // convert search result to collection of dynamic objects
                //
                dynamic pets = JsonConvert.DeserializeObject(searchResult);

                //
                // Get a reference to my cosmosdb document collection
                //
                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(dbName, collName);

                //
                // list to keep track of new pets
                //
                List<dynamic> newPets = new List<dynamic>();

                //
                // for each pet in the search results
                //
                foreach (dynamic pet in pets.animals)
                {
                    log.LogInformation($"{pet.id}:{pet.name}");

                    //
                    // check to see if we have seen this pet before
                    //
                    var query = petsClient.CreateDocumentQuery(collectionUri, $"select * from c where c.petId = '{pet.id}'").AsEnumerable();   
                    if(query.FirstOrDefault() != null)
                    {
                        //
                        // we've already seen this one.. move on
                        //
                        log.LogInformation("Pet Found");
                    }
                    else
                    {
                        //
                        // we have not seen this pet -- add it to cosmosdb and the collection that we will return to the caller
                        //
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
                    }

                }

                // return the list of new pets
                return new OkObjectResult(newPets);
            }
            catch(Exception e)
            {
                log.LogError(e.Message);
                log.LogError(e.ToString());
                return new BadRequestObjectResult(e.ToString());
            }
        }

        private static async Task<string> GetToken(ILogger log)
        {
            log.LogInformation("Getting Token");
            string logonUri = Environment.GetEnvironmentVariable("LogonUri");
            string apiKey = Environment.GetEnvironmentVariable("ApiKey");
            var client = new RestClient(logonUri);
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", $"Basic {apiKey}");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "client_credentials");
            IRestResponse response = await client.ExecuteAsync(request);

            dynamic responseObj = JsonConvert.DeserializeObject(response.Content);
            //Console.WriteLine(response.Content);
            log.LogInformation("Token Generated");
            return responseObj.access_token;
        }

        private static async Task<string> ExecuteSearch(string token, ILogger log)
        {
            log.LogInformation("Executing Search");
            string searchUri = Environment.GetEnvironmentVariable("searchUri");
            string searchQuery = Environment.GetEnvironmentVariable("searchQuery");
            log.LogInformation($"searchQuery = {searchUri}{searchQuery}");
            var client = new RestClient($"{searchUri}{searchQuery}");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", $"Bearer {token}");
            IRestResponse response = await client.ExecuteAsync(request);
            //Console.WriteLine(response.Content);
            log.LogInformation("Serach Complete");
            return response.Content;

        }
    }

}

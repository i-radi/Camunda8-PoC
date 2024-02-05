using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DTOs;
using fastJSON;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Zeebe.Client.Impl.Builder;

namespace Cloudstarter.Services;

public class ZeebeService: IZeebeService
{
    private readonly IZeebeClient _client;
    private readonly ILogger<ZeebeService> _logger;
    private readonly IWebHostEnvironment _env;

    public ZeebeService(IConfiguration config, ILogger<ZeebeService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;

        var authServer = config["ZeebeClientCloudSettings:OAuthURL"];
        var clientId = config["ZeebeClientCloudSettings:ClientId"];
        var clientSecret = config["ZeebeClientCloudSettings:ClientSecret"];
        var zeebeUrl = config["ZeebeClientCloudSettings:ClusterURL"];
        char[] port = {'4', '3', ':'};
        var audience = zeebeUrl?.TrimEnd(port);
        
        _client =
            ZeebeClient.Builder()
                .UseGatewayAddress(zeebeUrl)
                .UseTransportEncryption()
                .UseAccessTokenSupplier(
                    CamundaCloudTokenProvider.Builder()
                        .UseAuthServer(authServer)
                        .UseClientId(clientId)
                        .UseClientSecret(clientSecret)
                        .UseAudience(audience)
                        .Build())
                .Build();
    }

    public Task<ITopology> Status()
    {
        return _client.TopologyRequest().Send();
    }
   
    public async Task<IDeployResourceResponse> Deploy(string modelFile)
    {
        var filename = Path.Combine(_env.ContentRootPath, "Resources", modelFile);
        var deployment = await _client.NewDeployCommand().AddResourceFile(filename).Send();
        var res = deployment.Processes[0];
        _logger.LogInformation("Deployed BPMN Model: " + res?.BpmnProcessId +
                    " v." + res?.Version);
        return deployment;
    }
    
    public void StartWorkers()
    {
        CreateGetTimeWorker();
        CreateMakeGreetingWorker();
    }

    public void CreateGetTimeWorker()
    {
        _createWorker("get-time", async (client, job) =>
        {
            _logger.LogInformation("Received job: " + job);
                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.GetAsync("https://timeapi.io/api/Time/current/zone?timeZone=Europe/Amsterdam"))
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        
                        await client.NewCompleteJobCommand(job.Key)
                            .Variables("{\"time\":" + apiResponse + "}")
                            .Send();
                        _logger.LogInformation("Get Time worker completed");
                    }
                }
        });    
    }
    
    public void CreateMakeGreetingWorker()
    {
        _createWorker("make-greeting", async (client, job) =>
        {
            _logger.LogInformation("Make Greeting Received job: " + job);
            var headers = JSON.ToObject<MakeGreetingCustomHeadersDto>(job.CustomHeaders);
            var variables = JSON.ToObject<MakeGreetingVariablesDTO>(job.Variables);
            string greeting = headers.greeting;
            string name = variables.name;

            await client.NewCompleteJobCommand(job.Key)
                .Variables("{\"say\": \"" + greeting + " " + name + "\"}")
                .Send();
            _logger.LogInformation("Make Greeting Worker completed job");
        });    
    }

    public async Task<String> CreateWorkflowInstance(string bpmProcessId)
    {
        _logger.LogInformation("Creating workflow instance...");
        var instance = await _client.NewCreateProcessInstanceCommand()
            .BpmnProcessId(bpmProcessId)
            .LatestVersion()
            .Variables("{\"name\": \"7mada\"}")
            .WithResult()
            .Send();
        var jsonParams = new JSONParameters {ShowReadOnlyProperties = true};
        return JSON.ToJSON(instance, jsonParams);
    }
   
    private void _createWorker(String jobType, JobHandler handleJob)
    {
        _client.NewWorker()
                .JobType(jobType)
                .Handler(handleJob)
                .MaxJobsActive(5)
                .Name(jobType)
                .PollInterval(TimeSpan.FromSeconds(50))
                .PollingTimeout(TimeSpan.FromSeconds(50))
                .Timeout(TimeSpan.FromSeconds(10))
                .Open();
    }
}
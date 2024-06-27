using System.Threading.Tasks;
using Zeebe.Client.Api.Responses;

namespace Cloudstarter.Services
{
    public interface IZeebeService
    {
        public Task<ITopology> Status();
        public Task<IDeployResourceResponse> Deploy(string modelFile);
        public Task StartWorkers();
        public Task<string> CreateWorkflowInstance(string bpmProcessId);

    }
}
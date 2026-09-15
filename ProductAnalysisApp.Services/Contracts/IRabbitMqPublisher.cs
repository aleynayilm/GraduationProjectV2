using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Contracts
{
    public interface IRabbitMqPublisher
    {
        Task<string> PublishAsync(string queueName, object payload);
    }
}   

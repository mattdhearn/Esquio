﻿using Esquio.UI.Api.Diagnostics;
using Esquio.UI.Api.Infrastructure.Data.DbContexts;
using Esquio.UI.Api.Infrastructure.Data.Entities;
using Esquio.UI.Api.Shared.Models.Features.Rollout;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Esquio.UI.Api.Scenarios.Flags.Rollout
{
    internal class RolloutFeatureRequestHandler
        : IRequestHandler<RolloutFeatureRequest>
    {
        private readonly StoreDbContext _storeDbContext;
        private readonly ILogger<RolloutFeatureRequestHandler> _logger;

        public RolloutFeatureRequestHandler(StoreDbContext storeDbContext, ILogger<RolloutFeatureRequestHandler> logger)
        {
            _storeDbContext = storeDbContext ?? throw new ArgumentNullException(nameof(storeDbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public async Task<Unit> Handle(RolloutFeatureRequest request, CancellationToken cancellationToken)
        {
            var feature = await _storeDbContext
                .Features
                .Include(f => f.ProductEntity)  // -> this is only needed for "history"
                .Where(f => f.Name == request.FeatureName && f.ProductEntity.Name == request.ProductName)
                .Include(f => f.Toggles)
                .SingleOrDefaultAsync(cancellationToken);

            var deployment = await _storeDbContext
                .Deployments
                .Include(r => r.ProductEntity)
                .Where(r => r.Name == request.DeploymentName &&  r.ProductEntity.Name == request.ProductName)
                .SingleOrDefaultAsync();

            if (feature != null && deployment != null)
            {
                var currentState = await _storeDbContext.FeatureStates
                    .Where(fs => fs.FeatureEntityId == feature.Id && fs.DeploymentEntityId == deployment.Id)
                    .SingleOrDefaultAsync();

                if (currentState != null)
                {
                    currentState.Enabled = true;
                }
                else
                {
                    _storeDbContext.FeatureStates.Add(new FeatureStateEntity()
                    {
                        DeploymentEntityId = deployment.Id,
                        FeatureEntityId = feature.Id,
                        Enabled = true
                    });
                }

                await _storeDbContext.SaveChangesAsync(cancellationToken);
                return Unit.Value;
            }

            Log.FeatureNotExist(_logger, request.FeatureName.ToString());
            throw new InvalidOperationException("Operation can't be performed because the combination feature product and deployment are not valid on this store.");
        }
    }
}

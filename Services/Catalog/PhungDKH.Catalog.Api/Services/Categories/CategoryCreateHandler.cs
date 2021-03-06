﻿namespace PhungDKH.Catalog.Service.Categories
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoMapper;
    using MediatR;
    using PhungDKH.EvenBus.Abstractions;
    using PhungDKH.Catalog.Domain.Entities;
    using PhungDKH.Catalog.Domain.Entities.Contexts;
    using PhungDKH.Catalog.Service.Categories.Events;
    using PhungDKH.Core.Models.Common;

    public class CategoryCreateHandler : IRequestHandler<CategoryCreateRequest, ResponseModel>
    {
        private readonly AppCatalogDbContext db;
        private readonly IMapper mapper;
        private readonly IEventBus eventBus;

        /// <summary>
        ///   Initializes a new instance of the <see cref="CategoryCreateHandler" /> class.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="mapper">The auto mapper configuration.</param>
        public CategoryCreateHandler(
            AppCatalogDbContext db,
            IMapper mapper,
            IEventBus eventBus)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public async Task<ResponseModel> Handle(CategoryCreateRequest request, CancellationToken cancellationToken)
        {
            var category = this.mapper.Map<Category>(request);

            this.db.Categories.Add(category);

            await this.db.SaveChangesAsync(cancellationToken);

            var eventPost = new CategoryCreatedEvent(request.Name);
            this.eventBus.Publish(eventPost);

            return new ResponseModel()
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Message = "The category was created successfully"
            };
        }
    }
}

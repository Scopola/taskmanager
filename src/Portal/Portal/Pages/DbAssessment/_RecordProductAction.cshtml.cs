﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Portal.Models;
using WorkflowDatabase.EF;
using WorkflowDatabase.EF.Models;

namespace Portal.Pages.DbAssessment
{
    public class _RecordProductActionModel : PageModel
    {
        private readonly WorkflowDbContext _dbContext;

        [BindProperty(SupportsGet = true)]
        public int ProcessId { get; set; }

        [DisplayName("Action:")]
        public bool ProductActioned { get; set; }

        [DisplayName("Change:")]
        public string ProductActionChangeDetails { get; set; }

        public List<ProductAction> ProductActions { get; set; }

        public SelectList ImpactedProducts { get; set; }

        public SelectList ProductActionTypes { get; set; }

        public _RecordProductActionModel(WorkflowDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        public async Task OnGetAsync(int processId)
        {
            SetHpdProducts();
            await PopulateProductActionTypes();
            await SetProductActionFromDb();
            await SetActionedDummyData();
        }

        private async Task PopulateProductActionTypes()
        {
            var productActionTypes = await _dbContext.ProductActionType.ToListAsync();
            ProductActionTypes = new SelectList(productActionTypes, nameof(ProductActionType.ProductActionTypeId), nameof(ProductActionType.Name));
        }

        private void SetHpdProducts()
        {
            ImpactedProducts = new SelectList(
                new List<ImpactedProduct>
                {
                    new ImpactedProduct {ProductId = 0, Product = "Select..."},
                    new ImpactedProduct {ProductId = 1, Product = "GB123456"},
                    new ImpactedProduct {ProductId = 2, Product = "GB111222"},
                    new ImpactedProduct {ProductId = 3, Product = "GB987651"}
                }, "ProductId", "Product");

        }

        private async Task SetProductActionFromDb()
        {
            //var productActionTypes = await _dbContext.ProductActionType.ToListAsync();

            ProductActions = await _dbContext.ProductAction
                .Include(p => p.ProductActionType)
                .Where(pa => pa.ProcessId == ProcessId)
                .ToListAsync();

        }

        private async Task SetActionedDummyData()
        {
            var dbAssessmentAssessData = await _dbContext.DbAssessmentAssessData.FirstOrDefaultAsync(p => p.ProcessId == ProcessId);

            if (dbAssessmentAssessData != null)
            {
                ProductActioned = dbAssessmentAssessData.ProductActioned;
                ProductActionChangeDetails = dbAssessmentAssessData.ProductActionChangeDetails;
            }

        }

    }
}
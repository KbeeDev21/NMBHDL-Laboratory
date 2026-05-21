using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace WebApplication2.Controllers
{
    public class LabManagerController : Controller
    {
        private readonly ILogger<LabManagerController> _logger;

        public LabManagerController(ILogger<LabManagerController> logger)
        {
            _logger = logger;
        }
        public IActionResult ConsumableIndex()
        {
            {
                return PartialView("ConsumableIndex");
            }
        }
        public IActionResult DoctorIndex()
        {
            {
                return PartialView("DoctorIndex");
            }
        }
        public IActionResult DoctorCreate()
        {
            {
                return PartialView("DoctorCreate");
            }
        }
        public IActionResult OrderCreate()
        {
            {
                return PartialView("OrderCreate");
            }
        }
        public IActionResult OrderIndex()
        {
            {
                return PartialView("OrderIndex");
            }
        }
        public IActionResult SampleTypeIndex()
        {
            {
                return PartialView("SampleTypeIndex");
            }
        }
        public IActionResult SupplierCreate()
        {
            {
                return PartialView("SupplierCreate");
            }
        }
        public IActionResult SupplierIndex()
        {
            {
                return PartialView("SupplierIndex");
            }
        }
        public IActionResult TechnicianIndex()
        {
            {
                return PartialView("TechnicianIndex");
            }
        }
        public IActionResult TestCategoryCreate()
        {
            {
                return PartialView("TestCategoryCreate");
            }
        }
        public IActionResult TestCategoryEdit()
        {
            {
                return PartialView("TestCategoryEdit");
            }
        }
        public IActionResult TestCategoryIndex()
        {
            {
                return PartialView("TestCategoryIndex");
            }
        }
        public IActionResult TestTypeCreate()
        {
            {
                return PartialView("TestTypeCreate");
            }
        }
        public IActionResult TestTypeIndex()
        {
            {
                return PartialView("TestTypeIndex");
            }
        }
    }
    
}

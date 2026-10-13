using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace WebApplication2.Controllers
{
    // keep in mind to look over dashboard , profile , changepassword , editprofile views they are not responding

    public class LabManagerController : Controller
    {
        private readonly ILogger<LabManagerController> _logger;

        public LabManagerController(ILogger<LabManagerController> logger)
        {
            _logger = logger;
        }
        public IActionResult Dashboard()
        {
            {
                return PartialView("Dashboard");
            }
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
       public IActionResult Profile()
        {
            {
                return PartialView("Profile");
            }
        }
        public IActionResult ChangePassword()
        {
            {
                return PartialView("ChangePassword");
            }
        }
        public IActionResult EditProfile()
        {
            {
                return PartialView("EditProfile");
            }
        }
    }

}

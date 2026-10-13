//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Collections.Generic;

//namespace WebApplication2.Controllers
//{
//    // keep in mind to look over dashboard , profile , changepassword , editprofile views they are not responding

//    public class LabManagerController : Controller
//    {
//        private readonly ILogger<LabManagerController> _logger;

//        public LabManagerController(ILogger<LabManagerController> logger)
//        {
//            _logger = logger;
//        }
//        public IActionResult Dashboard()
//        {
//            {
//                return PartialView("Dashboard");
//            }
//        }
//        public IActionResult ConsumableIndex()
//        {
//            {
//                return PartialView("ConsumableIndex");
//            }
//        }
//        public IActionResult DoctorIndex()
//        {
//            {
//                return PartialView("DoctorIndex");
//            }
//        }
//        public IActionResult DoctorCreate()
//        {
//            {
//                return PartialView("DoctorCreate");
//            }
//        }
//        public IActionResult OrderCreate()
//        {
//            {
//                return PartialView("OrderCreate");
//            }
//        }
//        public IActionResult OrderIndex()
//        {
//            {
//                return PartialView("OrderIndex");
//            }
//        }
//        public IActionResult SampleTypeIndex()
//        {
//            {
//                return PartialView("SampleTypeIndex");
//            }
//        }
//        public IActionResult SupplierCreate()
//        {
//            {
//                return PartialView("SupplierCreate");
//            }
//        }
//        public IActionResult SupplierIndex()
//        {
//            {
//                return PartialView("SupplierIndex");
//            }
//        }
//        public IActionResult TechnicianIndex()
//        {
//            {
//                return PartialView("TechnicianIndex");
//            }
//        }
//        public IActionResult TestCategoryCreate()
//        {
//            {
//                return PartialView("TestCategoryCreate");
//            }
//        }
//        public IActionResult TestCategoryEdit()
//        {
//            {
//                return PartialView("TestCategoryEdit");
//            }
//        }
//        public IActionResult TestCategoryIndex()
//        {
//            {
//                return PartialView("TestCategoryIndex");
//            }
//        }
//        public IActionResult TestTypeCreate()
//        {
//            {
//                return PartialView("TestTypeCreate");
//            }
//        }
//        public IActionResult TestTypeIndex()
//        {
//            {
//                return PartialView("TestTypeIndex");
//            }
//        }
//       public IActionResult Profile()
//        {
//            {
//                return PartialView("Profile");
//            }
//        }
//        public IActionResult ChangePassword()
//        {
//            {
//                return PartialView("ChangePassword");
//            }
//        }
//        public IActionResult EditProfile()
//        {
//            {
//                return PartialView("EditProfile");
//            }
//        }
//    }

//}
using Microsoft.AspNetCore.Mvc;

namespace WebApplication2.Controllers
{
    /// <summary>
    /// Laboratory Manager subsystem (NMB-HLabSys).
    ///
    /// Covers User Stories US-LM-01 to US-LM-09.
    ///
    /// There is no database yet, so each POST action validates what can be checked
    /// without one (required fields, number ranges, password rules) and then reports
    /// back through TempData["Success"] / TempData["Error"], which every view displays
    /// in its banner. Where a check needs the database, the place is marked
    /// "TODO (DB)" together with the acceptance criterion it belongs to.
    /// </summary>
    public class LabManagerController : Controller
    {
        private readonly ILogger<LabManagerController> _logger;

        public LabManagerController(ILogger<LabManagerController> logger)
        {
            _logger = logger;
        }

        // =====================================================================
        // OVERVIEW
        // =====================================================================

        public IActionResult Dashboard()
        {
            return View();
        }

        // =====================================================================
        // US-LM-01  MANAGE TEST CATEGORIES
        // =====================================================================

        public IActionResult TestCategoryIndex()
        {
            // TODO (DB): AC-01-01 load all categories with name and description.
            return View();
        }

        public IActionResult TestCategoryCreate()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TestCategoryCreate(string categoryName, string description)
        {
            // AC-01-04 / TC-LM-01-03: blank name is rejected.
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                TempData["Error"] = "Category name is required.";
                return View();
            }

            // TODO (DB): AC-01-03 / TC-LM-01-02 reject a duplicate name.
            //   if (_db.CategoryNameExists(categoryName))
            //   {
            //       TempData["Error"] = "Category name already exists";
            //       return View();
            //   }

            // TODO (DB): AC-01-02 insert the category.

            TempData["Success"] = $"Category '{categoryName}' was added.";
            return RedirectToAction(nameof(TestCategoryIndex));
        }

        public IActionResult TestCategoryEdit(int id)
        {
            // TODO (DB): AC-01-05 load this category so the form opens pre-filled.
            ViewBag.CategoryId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TestCategoryEdit(int categoryId, string categoryName, string description)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                TempData["Error"] = "Category name is required.";
                return View();
            }

            // TODO (DB): AC-01-03 a renamed category must still be unique.
            // TODO (DB): AC-01-06 / TC-LM-01-04 save the changes.

            TempData["Success"] = "Category updated.";
            return RedirectToAction(nameof(TestCategoryIndex));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TestCategoryDelete(int id)
        {
            // TODO (DB): AC-01-07 / TC-LM-01-05 block deletion when test types are linked.
            //   if (_db.CategoryHasTestTypes(id))
            //   {
            //       TempData["Error"] = "Category cannot be deleted as it has associated test types";
            //       return RedirectToAction(nameof(TestCategoryIndex));
            //   }

            TempData["Success"] = "Category deleted.";
            return RedirectToAction(nameof(TestCategoryIndex));
        }

        // =====================================================================
        // US-LM-02  MANAGE TEST TYPE RECORDS
        // =====================================================================

        public IActionResult TestTypeIndex()
        {
            // TODO (DB): AC-02-01 load test types with category, sample type, units,
            //            normal range and turnaround time.
            return View();
        }

        public IActionResult TestTypeCreate()
        {
            // TODO (DB): populate the category, sample type and consumable dropdowns.
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TestTypeCreate(
            string testName,
            int? categoryId,
            int? sampleTypeId,
            string units,
            decimal? normalRangeMin,
            decimal? normalRangeMax,
            int? turnaroundTime,
            int[] consumables)
        {
            // AC-02-06 / TC-LM-02-06: mandatory fields.
            if (string.IsNullOrWhiteSpace(testName) || categoryId == null || sampleTypeId == null
                || string.IsNullOrWhiteSpace(units) || turnaroundTime == null)
            {
                TempData["Error"] = "Please complete all required fields.";
                return View();
            }

            // AC-02-08 / TC-LM-02-03: min may not exceed max.
            if (normalRangeMin.HasValue && normalRangeMax.HasValue && normalRangeMin > normalRangeMax)
            {
                TempData["Error"] = "Min value cannot exceed max value";
                return View();
            }

            // TODO (DB): AC-02-03 / TC-LM-02-02 reject a duplicate test name.
            // TODO (DB): AC-02-02 insert the test type.
            // TODO (DB): AC-02-05 / TC-LM-02-04 link the selected consumables (many-to-many).

            TempData["Success"] = $"Test type '{testName}' was added.";
            return RedirectToAction(nameof(TestTypeIndex));
        }

        public IActionResult TestTypeEdit(int id)
        {
            // TODO (DB): load this test type and tick its linked consumables.
            ViewBag.TestTypeId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TestTypeEdit(
            int testTypeId,
            string testName,
            int? categoryId,
            int? sampleTypeId,
            string units,
            decimal? normalRangeMin,
            decimal? normalRangeMax,
            int? turnaroundTime,
            int[] consumables)
        {
            if (string.IsNullOrWhiteSpace(testName) || categoryId == null || sampleTypeId == null
                || string.IsNullOrWhiteSpace(units) || turnaroundTime == null)
            {
                TempData["Error"] = "Please complete all required fields.";
                return View();
            }

            // AC-02-08: same range rule applies on edit.
            if (normalRangeMin.HasValue && normalRangeMax.HasValue && normalRangeMin > normalRangeMax)
            {
                TempData["Error"] = "Min value cannot exceed max value";
                return View();
            }

            // TODO (DB): save the test type and refresh its consumable links.

            TempData["Success"] = "Test type updated.";
            return RedirectToAction(nameof(TestTypeIndex));
        }

        /// <summary>
        /// AC-02-07 / TC-LM-02-05 — the test type with the technicians qualified to perform it.
        /// </summary>
        public IActionResult TestTypeDetail(int id)
        {
            // TODO (DB): load the test type, its consumables, and the technicians
            //            assigned to it.
            ViewBag.TestTypeId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TestTypeDelete(int id)
        {
            // TODO (DB): block deletion when results or orders reference this test type.
            TempData["Success"] = "Test type deleted.";
            return RedirectToAction(nameof(TestTypeIndex));
        }

        // =====================================================================
        // SAMPLE TYPES  (supports AC-02-09)
        // =====================================================================

        public IActionResult SampleTypeIndex()
        {
            // TODO (DB): load all sample types.
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SampleTypeCreate(string sampleTypeName)
        {
            if (string.IsNullOrWhiteSpace(sampleTypeName))
            {
                TempData["Error"] = "Sample type name is required.";
                return RedirectToAction(nameof(SampleTypeIndex));
            }

            // TODO (DB): reject a duplicate name, then insert.

            TempData["Success"] = $"Sample type '{sampleTypeName}' was added.";
            return RedirectToAction(nameof(SampleTypeIndex));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SampleTypeDelete(int id)
        {
            // TODO (DB): block deletion when test types use this sample type.
            TempData["Success"] = "Sample type deleted.";
            return RedirectToAction(nameof(SampleTypeIndex));
        }

        // =====================================================================
        // US-LM-03  MANAGE CONSUMABLES
        // =====================================================================

        public IActionResult ConsumableIndex()
        {
            // TODO (DB): AC-03-01 load consumables with quantity on hand, reorder level
            //            and supplier.
            // TODO (DB): AC-03-08 / TC-LM-03-06 flag any consumable at or within 10%
            //            above its reorder level.
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConsumableCreate(
            string consumableName,
            int? supplierId,
            int reorderLevel,
            int quantityOnHand)
        {
            if (string.IsNullOrWhiteSpace(consumableName) || supplierId == null)
            {
                TempData["Error"] = "Consumable name and supplier are required.";
                return RedirectToAction(nameof(ConsumableIndex));
            }

            if (reorderLevel < 0 || quantityOnHand < 0)
            {
                TempData["Error"] = "Reorder level and quantity cannot be negative.";
                return RedirectToAction(nameof(ConsumableIndex));
            }

            // TODO (DB): AC-03-03 reject a duplicate consumable name.
            // TODO (DB): AC-03-02 / TC-LM-03-01 insert the consumable.

            TempData["Success"] = $"Consumable '{consumableName}' was added.";
            return RedirectToAction(nameof(ConsumableIndex));
        }

        public IActionResult ConsumableEdit(int id)
        {
            // TODO (DB): load this consumable.
            ViewBag.ConsumableId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConsumableEdit(
            int consumableId,
            string consumableName,
            int? supplierId,
            int reorderLevel)
        {
            if (string.IsNullOrWhiteSpace(consumableName) || supplierId == null)
            {
                TempData["Error"] = "Consumable name and supplier are required.";
                return View();
            }

            if (reorderLevel < 0)
            {
                TempData["Error"] = "Reorder level cannot be negative.";
                return View();
            }

            // TODO (DB): save the changes, keeping the name unique.

            TempData["Success"] = "Consumable updated.";
            return RedirectToAction(nameof(ConsumableIndex));
        }

        /// <summary>
        /// AC-03-04 / 05 / 06 / 07 — increase, decrease or set the quantity on hand.
        /// The view confirms the adjustment before posting here.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdjustStock(int consumableId, string action, int amount)
        {
            if (amount < 0)
            {
                TempData["Error"] = "Adjustment amount cannot be negative.";
                return RedirectToAction(nameof(ConsumableIndex));
            }

            // TODO (DB): read the real quantity. 0 is a placeholder until then.
            int currentQuantity = 0;
            int newQuantity;

            switch (action)
            {
                case "Increase":                        // AC-03-04 / TC-LM-03-02
                    newQuantity = currentQuantity + amount;
                    break;

                case "Decrease":                        // AC-03-05 / TC-LM-03-03
                    newQuantity = currentQuantity - amount;
                    break;

                case "Set":                             // AC-03-07 / TC-LM-03-05
                    newQuantity = amount;
                    break;

                default:
                    TempData["Error"] = "Unknown stock action.";
                    return RedirectToAction(nameof(ConsumableIndex));
            }

            // AC-03-06 / TC-LM-03-04: stock may never go below zero.
            if (newQuantity < 0)
            {
                TempData["Error"] = "Stock cannot go below zero";
                return RedirectToAction(nameof(ConsumableIndex));
            }

            // TODO (DB): write newQuantity back for consumableId.

            TempData["Success"] = $"Stock updated to {newQuantity}.";
            return RedirectToAction(nameof(ConsumableIndex));
        }

        // =====================================================================
        // US-LM-04  MANAGE SUPPLIERS
        // =====================================================================

        public IActionResult SupplierIndex()
        {
            // TODO (DB): AC-04-01 load suppliers with contact person and email.
            return View();
        }

        public IActionResult SupplierCreate()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SupplierCreate(string supplierName, string contactPerson, string email)
        {
            if (string.IsNullOrWhiteSpace(supplierName) || string.IsNullOrWhiteSpace(contactPerson))
            {
                TempData["Error"] = "Supplier name and contact person are required.";
                return View();
            }

            // AC-04-04 / TC-LM-04-03: the email must be a valid address.
            if (!IsValidEmail(email))
            {
                TempData["Error"] = "Invalid email address";
                return View();
            }

            // TODO (DB): AC-04-03 / TC-LM-04-02 reject a duplicate supplier name.
            // TODO (DB): AC-04-02 / TC-LM-04-01 insert the supplier.

            TempData["Success"] = $"Supplier '{supplierName}' was added.";
            return RedirectToAction(nameof(SupplierIndex));
        }

        public IActionResult SupplierEdit(int id)
        {
            // TODO (DB): load this supplier.
            ViewBag.SupplierId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SupplierEdit(int supplierId, string supplierName, string contactPerson, string email)
        {
            if (string.IsNullOrWhiteSpace(supplierName) || string.IsNullOrWhiteSpace(contactPerson))
            {
                TempData["Error"] = "Supplier name and contact person are required.";
                return View();
            }

            if (!IsValidEmail(email))
            {
                TempData["Error"] = "Invalid email address";
                return View();
            }

            // TODO (DB): AC-04-05 / TC-LM-04-05 save the changes.

            TempData["Success"] = "Supplier updated.";
            return RedirectToAction(nameof(SupplierIndex));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SupplierDelete(int id)
        {
            // TODO (DB): AC-04-06 / TC-LM-04-04 block deletion when consumables are linked.
            //   if (_db.SupplierHasConsumables(id))
            //   {
            //       TempData["Error"] = "Supplier has associated consumables and cannot be deleted";
            //       return RedirectToAction(nameof(SupplierIndex));
            //   }

            TempData["Success"] = "Supplier deleted.";
            return RedirectToAction(nameof(SupplierIndex));
        }

        // =====================================================================
        // US-LM-05  MANAGE CONSUMABLE ORDERS
        // =====================================================================

        public IActionResult OrderIndex()
        {
            // TODO (DB): AC-05-09 load each order with its number, status, supplier
            //            and the status of every item.
            return View();
        }

        public IActionResult OrderCreate()
        {
            // TODO (DB): AC-05-01 / TC-LM-05-07 load consumables at or within 10% of
            //            their reorder level, grouped by supplier.
            // TODO (DB): generate the next order number.
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult OrderCreate(string orderNumber, int? supplierId, List<OrderItemInput> orderItems)
        {
            if (supplierId == null)
            {
                TempData["Error"] = "Please choose a supplier.";
                return View();
            }

            var selected = orderItems?.Where(i => i.Selected).ToList() ?? new List<OrderItemInput>();

            if (selected.Count == 0)
            {
                TempData["Error"] = "Select at least one consumable to order.";
                return View();
            }

            if (selected.Any(i => i.Quantity <= 0))
            {
                TempData["Error"] = "Order quantities must be greater than zero.";
                return View();
            }

            // TODO (DB): AC-05-02 save the order with a unique order number and
            //            status 'Ordered'.
            // TODO (EMAIL): AC-05-03 / TC-LM-05-01 email the supplier the order details.

            TempData["Success"] = $"Order {orderNumber} was placed and emailed to the supplier.";
            return RedirectToAction(nameof(OrderIndex));
        }

        /// <summary>
        /// AC-05-09 — one order with every item's individual status.
        /// </summary>
        public IActionResult OrderDetail(int id)
        {
            // TODO (DB): load the order and its items.
            ViewBag.OrderId = id;
            return View();
        }

        /// <summary>
        /// AC-05-06 / TC-LM-05-06 — receiving an item raises that consumable's stock.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult OrderItemReceive(int id)
        {
            // TODO (DB): AC-05-06 mark the item received, capture the received date and
            //            increase the consumable's quantity on hand by the quantity ordered.
            // TODO (DB): AC-05-04 / TC-LM-05-02 if every item is now received, set the
            //            order to 'Complete' and capture the completion date.
            // TODO (DB): AC-05-05 / TC-LM-05-03 otherwise set it to 'Partially Complete'.

            TempData["Success"] = "Item marked as received and stock updated.";
            return RedirectToAction(nameof(OrderDetail), new { id });
        }

        /// <summary>
        /// AC-05-08 / TC-LM-05-05 — cancel a single item, with a reason.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult OrderItemCancel(int id, string cancellationReason)
        {
            if (string.IsNullOrWhiteSpace(cancellationReason))
            {
                TempData["Error"] = "A reason is required to cancel an item.";
                return RedirectToAction(nameof(OrderDetail), new { id });
            }

            // TODO (DB): set this item to 'Cancelled' with the date and reason.
            // TODO (EMAIL): notify the supplier.

            TempData["Success"] = "Item cancelled and the supplier was notified.";
            return RedirectToAction(nameof(OrderDetail), new { id });
        }

        /// <summary>
        /// AC-05-07 / TC-LM-05-04 — cancel a whole order, with a reason.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult OrderCancel(int id, string cancellationReason)
        {
            if (string.IsNullOrWhiteSpace(cancellationReason))
            {
                TempData["Error"] = "A reason is required to cancel an order.";
                return RedirectToAction(nameof(OrderDetail), new { id });
            }

            // TODO (DB): set the order to 'Cancelled' with the date and reason.
            // TODO (EMAIL): notify the supplier.

            TempData["Success"] = "Order cancelled and the supplier was notified.";
            return RedirectToAction(nameof(OrderIndex));
        }

        // =====================================================================
        // US-LM-06  MANAGE DOCTOR USERS
        // =====================================================================

        public IActionResult DoctorIndex()
        {
            // TODO (DB): AC-06-01 load doctors with name, surname, HPCSA number,
            //            email and contact number.
            return View();
        }

        public IActionResult DoctorCreate()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DoctorCreate(
            string name,
            string surname,
            string hpcsaNumber,
            string email,
            string contactNumber)
        {
            // AC-06-07 / TC-LM-06-05: every field is required.
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname)
                || string.IsNullOrWhiteSpace(hpcsaNumber) || string.IsNullOrWhiteSpace(contactNumber))
            {
                TempData["Error"] = "Please complete all required fields.";
                return View();
            }

            if (!IsValidEmail(email))
            {
                TempData["Error"] = "Invalid email address";
                return View();
            }

            // TODO (DB): AC-06-04 / TC-LM-06-02 reject a duplicate HPCSA number
            //            with "HPCSA number already registered".
            // TODO (DB): AC-06-05 / TC-LM-06-03 reject a duplicate email
            //            with "Email address already in use".
            // TODO (AUTH): AC-06-02 / TC-LM-06-01 create the account with an
            //            auto-generated password and flag it must-change-on-first-login
            //            (AC-06-03 / TC-LM-06-04).
            // TODO (EMAIL): send the password to the doctor.

            TempData["Success"] = $"Doctor {name} {surname} was registered and emailed a password.";
            return RedirectToAction(nameof(DoctorIndex));
        }

        public IActionResult DoctorEdit(int id)
        {
            // TODO (DB): load this doctor.
            ViewBag.DoctorId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DoctorEdit(
            int doctorId,
            string name,
            string surname,
            string hpcsaNumber,
            string email,
            string contactNumber)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname)
                || string.IsNullOrWhiteSpace(hpcsaNumber) || string.IsNullOrWhiteSpace(contactNumber))
            {
                TempData["Error"] = "Please complete all required fields.";
                return View();
            }

            if (!IsValidEmail(email))
            {
                TempData["Error"] = "Invalid email address";
                return View();
            }

            // TODO (DB): AC-06-06 save the changes, keeping the HPCSA number and
            //            email unique.

            TempData["Success"] = "Doctor record updated.";
            return RedirectToAction(nameof(DoctorIndex));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DoctorResetPassword(int doctorId)
        {
            // TODO (AUTH): generate a new password, flag must-change-on-first-login,
            //              and email it to the doctor.
            TempData["Success"] = "A new password was emailed to the doctor.";
            return RedirectToAction(nameof(DoctorEdit), new { id = doctorId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DoctorDelete(int id)
        {
            // TODO (DB): block deletion when the doctor has test requests on record.
            TempData["Success"] = "Doctor account removed.";
            return RedirectToAction(nameof(DoctorIndex));
        }

        // =====================================================================
        // US-LM-07  MANAGE LAB TECHNICIAN USERS
        // =====================================================================

        public IActionResult TechnicianIndex()
        {
            // TODO (DB): AC-07-01 load technicians with name, SA ID, employee number,
            //            contact, email and assigned test types.
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TechnicianCreate(
            string name,
            string surname,
            string saidNumber,
            string employeeNumber,
            string email,
            string contactNumber,
            int[] testTypes)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname)
                || string.IsNullOrWhiteSpace(employeeNumber) || string.IsNullOrWhiteSpace(contactNumber))
            {
                TempData["Error"] = "Please complete all required fields.";
                return RedirectToAction(nameof(TechnicianIndex));
            }

            if (!IsValidEmail(email))
            {
                TempData["Error"] = "Invalid email address";
                return RedirectToAction(nameof(TechnicianIndex));
            }

            // SA ID numbers are 13 digits.
            if (string.IsNullOrWhiteSpace(saidNumber) || saidNumber.Length != 13
                || !saidNumber.All(char.IsDigit))
            {
                TempData["Error"] = "SA ID number must be exactly 13 digits.";
                return RedirectToAction(nameof(TechnicianIndex));
            }

            // AC-07-03 / TC-LM-07-02: at least one test type must be assigned.
            if (testTypes == null || testTypes.Length == 0)
            {
                TempData["Error"] = "At least one test type must be assigned";
                return RedirectToAction(nameof(TechnicianIndex));
            }

            // TODO (DB): AC-07-04 / TC-LM-07-03 reject a duplicate SA ID with
            //            "ID number already registered".
            // TODO (AUTH): AC-07-02 / TC-LM-07-01 create the account with an
            //            auto-generated password and flag it must-change-on-first-login
            //            (AC-07-05 / TC-LM-07-04).
            // TODO (DB): AC-07-07 / TC-LM-07-06 link the selected test types. The same
            //            test type may be assigned to several technicians.
            // TODO (EMAIL): send the password to the technician.

            TempData["Success"] = $"Technician {name} {surname} was registered and emailed a password.";
            return RedirectToAction(nameof(TechnicianIndex));
        }

        public IActionResult TechnicianEdit(int id)
        {
            // TODO (DB): load this technician and tick their assigned test types.
            ViewBag.TechnicianId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TechnicianEdit(
            int technicianId,
            string name,
            string surname,
            string saidNumber,
            string employeeNumber,
            string email,
            string contactNumber,
            int[] testTypes)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname)
                || string.IsNullOrWhiteSpace(employeeNumber) || string.IsNullOrWhiteSpace(contactNumber))
            {
                TempData["Error"] = "Please complete all required fields.";
                return View();
            }

            if (!IsValidEmail(email))
            {
                TempData["Error"] = "Invalid email address";
                return View();
            }

            if (string.IsNullOrWhiteSpace(saidNumber) || saidNumber.Length != 13
                || !saidNumber.All(char.IsDigit))
            {
                TempData["Error"] = "SA ID number must be exactly 13 digits.";
                return View();
            }

            // AC-07-06 / TC-LM-07-05: the last test type may not be removed.
            if (testTypes == null || testTypes.Length == 0)
            {
                TempData["Error"] = "At least one test type must be assigned";
                return View();
            }

            // TODO (DB): AC-07-08 save the details and refresh the test type links.

            TempData["Success"] = "Technician record updated.";
            return RedirectToAction(nameof(TechnicianIndex));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TechnicianResetPassword(int technicianId)
        {
            // TODO (AUTH): generate a new password, flag must-change-on-first-login,
            //              and email it to the technician.
            TempData["Success"] = "A new password was emailed to the technician.";
            return RedirectToAction(nameof(TechnicianEdit), new { id = technicianId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TechnicianDelete(int id)
        {
            // TODO (DB): block deletion when the technician has results on record.
            TempData["Success"] = "Technician account removed.";
            return RedirectToAction(nameof(TechnicianIndex));
        }

        // =====================================================================
        // US-LM-08  GENERATE MANAGEMENT REPORTS
        // =====================================================================

        /// <summary>
        /// AC-08-01 — tests completed in a date range, grouped by category.
        /// The form posts by GET so the chosen range stays in the address bar.
        /// </summary>
        public IActionResult Reports(DateTime? startDate, DateTime? endDate)
        {
            // Nothing chosen yet: show the page with its default range.
            if (startDate == null || endDate == null)
            {
                return View();
            }

            // AC-08-03 / TC-LM-08-03: the range must run forwards.
            if (startDate > endDate)
            {
                TempData["Error"] = "Start date cannot be after end date";
                return View();
            }

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            // TODO (DB): AC-08-01 count completed tests per category in this range.
            // TODO (DB): AC-08-02 / TC-LM-08-02 when the range has no tests, the view
            //            shows "No tests found for the selected period".

            return View();
        }

        /// <summary>
        /// AC-08-05 / TC-LM-08-04 — the same report as a downloadable PDF.
        /// </summary>
        public IActionResult ReportsPdf(DateTime? startDate, DateTime? endDate)
        {
            if (startDate == null || endDate == null || startDate > endDate)
            {
                TempData["Error"] = "Choose a valid date range before downloading the report.";
                return RedirectToAction(nameof(Reports));
            }

            // TODO (PDF): build the PDF (QuestPDF or iTextSharp) containing the
            //             laboratory name, the date range, each test category with its
            //             count, and the total, then return it as a file:
            //
            //   byte[] pdf = _reportService.BuildCategoryReport(startDate.Value, endDate.Value);
            //   string fileName = $"LabReport_{startDate:yyyy-MM-dd}_to_{endDate:yyyy-MM-dd}.pdf";
            //   return File(pdf, "application/pdf", fileName);

            TempData["Error"] = "PDF generation is not implemented yet.";
            return RedirectToAction(nameof(Reports), new { startDate, endDate });
        }

        // =====================================================================
        // US-LM-09  LOGIN AND SECURITY  (manager account pages)
        // =====================================================================

        public IActionResult Profile()
        {
            // TODO (DB): load the signed-in manager's details.
            return View();
        }

        public IActionResult EditProfile()
        {
            // TODO (DB): load the signed-in manager's details.
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProfile(string name, string surname, string email, string contactNumber)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname)
                || string.IsNullOrWhiteSpace(contactNumber))
            {
                TempData["Error"] = "Please complete all required fields.";
                return View();
            }

            if (!IsValidEmail(email))
            {
                TempData["Error"] = "Invalid email address";
                return View();
            }

            // TODO (DB): save the manager's details.

            TempData["Success"] = "Your profile was updated.";
            return RedirectToAction(nameof(Profile));
        }

        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                TempData["Error"] = "Enter your current password.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Passwords do not match.";
                return View();
            }

            // AC-09-04 / TC-LM-09-04: complexity rules.
            if (!IsStrongPassword(newPassword))
            {
                TempData["Error"] = "Password must be at least 8 characters and include one uppercase letter, one number and one special character.";
                return View();
            }

            // TODO (AUTH): verify currentPassword, then save the new hash.

            TempData["Success"] = "Your password was changed.";
            return RedirectToAction(nameof(Profile));
        }

        /// <summary>
        /// AC-09-03 / TC-LM-09-03 — email a password reset link.
        /// </summary>
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(string email)
        {
            if (!IsValidEmail(email))
            {
                TempData["Error"] = "Invalid email address";
                return View();
            }

            // TODO (AUTH): if the address belongs to a manager, create a single-use
            //              reset token and email the link. Say the same thing either
            //              way, so the page cannot be used to discover which addresses
            //              are registered.

            TempData["Success"] = "If that address is registered, a reset link is on its way.";
            return View();
        }

        /// <summary>
        /// AC-09-04 — set a new password from the emailed link.
        /// </summary>
        public IActionResult ResetPassword(string token)
        {
            // TODO (AUTH): check the token is valid and unexpired.
            ViewBag.ResetToken = token;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(string resetToken, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Passwords do not match.";
                return View();
            }

            if (!IsStrongPassword(newPassword))
            {
                TempData["Error"] = "Password must be at least 8 characters and include one uppercase letter, one number and one special character.";
                return View();
            }

            // TODO (AUTH): check the token, save the new hash, and expire the token.

            TempData["Success"] = "Your password was reset. You can sign in now.";
            return RedirectToAction("LoginPage", "Home");
        }

        /// <summary>
        /// AC-09-07 / TC-LM-09-06 — shown when a non-manager opens a manager page.
        /// </summary>
        public IActionResult AccessDenied()
        {
            return View();
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        /// <summary>AC-04-04, AC-06-05 — basic email format check.</summary>
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                var address = new System.Net.Mail.MailAddress(email);
                return address.Address == email;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>AC-09-04 — 8+ characters, one uppercase, one number, one special.</summary>
        private static bool IsStrongPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
            {
                return false;
            }

            return password.Any(char.IsUpper)
                && password.Any(char.IsDigit)
                && password.Any(c => !char.IsLetterOrDigit(c));
        }
    }

    /// <summary>
    /// Binds the rows of the Create Order form, which post as
    /// OrderItems[0].ConsumableId, OrderItems[0].Selected, OrderItems[0].Quantity.
    /// </summary>
    public class OrderItemInput
    {
        public int ConsumableId { get; set; }
        public bool Selected { get; set; }
        public int Quantity { get; set; }
    }
}
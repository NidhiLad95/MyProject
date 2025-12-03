using BAL.Interface;
using BOL;
using Microsoft.AspNetCore.Mvc;

namespace GenxAi_Solutions.Controllers
{
    public class CompanyOnboardController : Controller
    {
        private readonly ICompanyProfileBAL _balObj;
        public CompanyOnboardController(ICompanyProfileBAL balObj)
        {
            _balObj = balObj;
        }
        //public IActionResult CompanyForm()
        //{
        //    return View();
        //}

        //suchita
        public async Task<IActionResult> CompanyForm(int? companyId)
        {
            CompanyProfile model = new CompanyProfile();
            var flged = 0;

            if (companyId.HasValue && companyId.Value > 0)
            {
                // Create request DTO
                var request = new GetByIdCompanyList
                {
                    CompanyId = companyId.Value
                };

                flged = 1;
                // Call BAL
                var response = await _balObj.GetByIdCompanyList(request);

                if (response != null && response.Data != null)
                {
                    //model = response.Data; // assign CompanyProfile from response
                    ViewBag.CompanyData = response.Data;
                }

                // ✅ Fetch SQL Analytics
                var sqlReq = new GetSQLConfigurationByCompanyId { CompanyID = companyId.Value };
                var sqlResponse = await _balObj.GetSQLConfigurationByCompanyId(sqlReq);
                if (sqlResponse?.Data != null)
                {
                    ViewBag.SqlConfig = sqlResponse.Data;
                }

                // ✅ Fetch File Analytics
                var fileResponse = await _balObj.GetFileConfigurationByCompanyId(sqlReq);
                
                if (fileResponse != null)
                {
                    ViewBag.FileConfig = fileResponse;
                }
            }

            ViewBag.Flgedit = flged;

            return View();
        }
        public IActionResult Chatbot()
        {
            return View();
        }

        public IActionResult CompanyMaster()
        {
            return View();
        }
    }
}

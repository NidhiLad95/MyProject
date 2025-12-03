using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOL
{
    [Serializable()]
    public class ScreenProperty
    {
        public int ScreenId { get; set; }
        public int ParentScreenid { get; set; }
        public string? Screenname { get; set; }
        public string? ScreenURL { get; set; }
        public string MenuIcon { get; set; }
        public int Sequence { get; set; }
        public int GroupId {  get; set; }
        //public string nIsActive { get; set; }
        //public string nIsActiveAction { get; set; }
        //public string ViewEdit { get; set; }
    }

    public class ScreenAccess
    {
        public int GroupId { get; set; }
    }

    public class DeleteScreenbyID
    {
        public int ScreenID { get; set; }
        public Guid UpdatedBy { get; set; }
    }

    public class MenuBind
    {
        public ScreenProperty ParentMenu { get; set; } = new();

        public List<ScreenProperty> ChildMenus { get; set; }= new();
    }
}

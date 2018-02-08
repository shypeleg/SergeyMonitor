using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DormRoomMonitor
{
    public class PersonManager
    {
        public static List<Person> personas;
        public static Person getPersonById(Guid id)
        {
            return personas.Find(p => p.faceApiId == id);
        }
    }
}

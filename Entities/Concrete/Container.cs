using Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete // Concrete is the folder in which objects themselves are restored.
{
    public class Container : IEntity  //The Container class and its properties were stated.
    {
        public virtual long Id { get; set; }
        public virtual string ContainerName { get; set; }
        public virtual double Latitude { get; set; }
        public virtual double Longitude { get; set; }
        public virtual long VehicleId { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace BulkOperations.Models
{
    public class Facility
    {
        public string Id { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double Co2Level { get; set; }
        public double TopLeftX { get; set; }
        public double TopLeftY { get; set; }
        public double TopRightX { get; set; }
        public double TopRightY { get; set; }
        public double BottomLeftX { get; set; }
        public double BottomLeftY { get; set; }
        public double BottomRightX { get; set; }
        public double BottomRightY { get; set; }
    }
}

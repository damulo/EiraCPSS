// See https://aka.ms/new-console-template for more information
using BulkOperations;

Console.WriteLine("Hello, World!");
// Console.WriteLine("Adding devices");
DevicesService devicesService = new DevicesService();
// await devicesService.AddDevices(4);
// Console.WriteLine("Adding devices. Done");

//await devicesService.AddDevices(20, 106);

//get twins
// var twins = await devicesService.GetDevicesAsTwins();

//foreach(var tw in twins)
//{
//    {
//        Console.WriteLine($"{tw.DeviceId}");
//        await devicesService.DeleteDevice(tw.DeviceId);
//        //Console.WriteLine($"\tReported properties: {tw.Properties.Reported}");
//        //Console.WriteLine($"Configurations: {tw.Configurations}");
        
//    }
//}

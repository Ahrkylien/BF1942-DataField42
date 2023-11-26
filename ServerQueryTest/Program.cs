Console.WriteLine("Console test for server query");

var server = new Bf1942ServerQuery("194.88.105.25", 23004);
var test = await server.Query(9999);

Console.WriteLine("end");
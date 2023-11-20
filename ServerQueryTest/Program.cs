Console.WriteLine("Console test for server query");

var server = new Bf1942ServerQuery("194.88.105.25", 23000);
var test = server.Query();

Console.WriteLine("end");
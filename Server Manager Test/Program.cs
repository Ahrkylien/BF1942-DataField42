Console.WriteLine("start");

var bfrm = new BfServerManagerClient("1.1.1.1", 15667, "username", "password");
await bfrm.Initialize(CancellationToken.None);
//bfrm.SayAll("hi all");
//await bfrm.SendConsoleCommand("Game.SayAll \"hi all\"", CancellationToken.None);
// await bfrm.GetAllMaps(CancellationToken.None);
//await bfrm.GetServerLog(CancellationToken.None);
//await bfrm.GetPlayers(CancellationToken.None);
await bfrm.GetUsers(CancellationToken.None);
//await bfrm.GetStatus(CancellationToken.None);

while (true)
    await Task.Delay(100);
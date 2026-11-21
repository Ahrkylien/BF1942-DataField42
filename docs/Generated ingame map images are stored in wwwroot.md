## Generated ingame map images are stored in wwwroot

### What happens
The "Analyse maps" admin job converts each map's ingame image and writes the result to
`wwwroot/images/ingamemaps/` inside the website's own folder
(`FileDatabase.InGameMapsDirectory`, built from `IWebHostEnvironment.WebRootPath`). The site serves
them from there at `/static/images/ingamemaps/{mapId}.{extension}`.

### Why this is bad practice
- **Application and data share a folder.** `wwwroot` belongs to the application: a deploy replaces
  it. The images are data produced at run time and are not in the repository. Data that has to
  survive a deploy does not belong in a folder that a deploy owns.
- **Every deploy has to work around it.** `setup-server.sh` runs `rsync --delete` with an exclusion
  for this folder, and the admin panel's update (`UpdateService`) has the same exclusion in its copy
  and delete steps. Any other way of deploying, or a change to either exclusion, deletes every
  generated image, and the only way to get them back is to run the job over the whole collection
  again.
- **It requires the application folder to be writable.** This works only because the service user
  owns the live folder. A read-only application folder or a container image breaks the job.
- **Data is split over two places.** The game files and the file transfer's working files live in
  the configured data folder, while these images live inside the application folder. A backup of
  the data folder misses them, and it is not obvious from the configuration that they exist.

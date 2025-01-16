
<h1 align="center">
  Web serial to Kindle next generation
  <br>
</h1>

<h4 align="center">A website to monitor web serials, crawl new chapters and send them to your Kindle</h4>

> [!WARNING]
> Please be aware that not all websites are suitable for crawling. Some sites may have restrictions or policies in place that prohibit automated access. Crawling such sites without permission can lead to legal consequences or the blocking of your IP address. Always check the website's robots.txt file and terms of service before initiating any crawling activities. If in doubt, seek permission from the website owner.
>
> Use at your own risk!

<p align="center">
  <a href="#key-features">Key Features</a> •
  <a href="#how-to-use">How To Use</a> •
  <a href="#license">License</a>
</p>

<img width="1316" alt="image" src="https://github.com/user-attachments/assets/ab75b874-5471-499d-a757-fa59ed0d5982" />

## Key Features

* Support various web serials sites
* Automatic detection for new chapters
* Supports password protected pages (i.e., chapters for Patreon supporters)
* Sends epubs to your Kindle via [email](https://www.amazon.com/sendtokindle/email)

## How To Use

Run the docker container:

```bash
docker run --name wstkng -p 8080:8080 -d ghcr.io/robinweitzel/wstkng:master
```

On first start-up go to the settings panel of the site to configure your email provider (from here emails will be sent) as well as your Kindle email address (here the emails will be sent to).
This has been tested using Google as an email provider, other providers should work but have not been tested.

## License

MIT

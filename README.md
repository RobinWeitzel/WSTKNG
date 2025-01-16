
<h1 align="center">
  Web serial to Kindle next generation
  <br>
</h1>

<h4 align="center">A website to monitor web serials, crawl new chapters and send them to your Kindle</h4>

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

Solution is provided as a Docker container:

```bash
docker run --name wstkng -p 8080:8080 -d ghcr.io/robinweitzel/wstkng:master
```

## License

MIT

"use strict";

// init local storage
const storage = new Storage();

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/notificationHub")
  .build();

const notification = document.getElementById("notification");
const notificationTitle = document.getElementById("notification-title");
const notificationText = document.getElementById("notification-text");
const classNames = ['enqueued', 'processing', 'succeeded', 'failed'];

const selectClass = (element, className) => {
  classNames.forEach((c) => {
    element.classList.remove(c);
  });
  element.classList.add(className);
};

connection.on("JobUpdate", function (jobs) {
  if (jobs.length > 0) {
    selectClass(notification, jobs[0].state.toLowerCase());
    notificationTitle.innerText = "Job " + jobs[0].state;
    notificationText.innerText = "Crawler method: " + jobs[0].name;

    notification.classList.add("flex");
    notification.classList.remove("hidden");
  } else {
    notification.classList.add("hidden");
    notification.classList.remove("flex");
  }

  for (const job of jobs) {
    if (job.state === "Succeeded" || job.state === "Failed" || job.state === "Deleted") {
      storage.removeId(job.id);
    }
  }
});

connection
  .start()
  .then(function () {
    setInterval(() => {
      connection
        .invoke("CheckForUpdate", storage.IdsToString())
        .catch(function (err) {
          return console.error(err.toString());
        });
    }, 1000);
  })
  .catch(function (err) {
    return console.error(err.toString());
  });

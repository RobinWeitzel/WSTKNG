// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
const toggleMenu = () => {
  const menu = document.getElementById('mobile-menu');

  menu.classList.toggle('hidden');
}

const showDialog = (id, title) => {
  const dialog = document.getElementById('dialog');
  const dialogInput = document.getElementById('modal-input');
  const dialogTitle = document.getElementById('modal-title');

  dialogInput.value = id;
  dialogTitle.innerText = "Delete " + title;

  dialog.classList.remove('hidden');
}

const hideDialog = () => {
  const dialog = document.getElementById('dialog');
  dialog.classList.add('hidden');
}
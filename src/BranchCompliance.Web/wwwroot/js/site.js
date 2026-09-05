document.querySelectorAll('.demo-account').forEach(button => {
    button.addEventListener('click', () => {
        document.getElementById('Email').value = button.dataset.email;
        document.getElementById('Password').value = 'PortfolioDemo123!';
        document.getElementById('Password').focus();
    });
});
document.querySelectorAll('form[data-confirm]').forEach(form => {
    form.addEventListener('submit', event => {
        if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    });
});

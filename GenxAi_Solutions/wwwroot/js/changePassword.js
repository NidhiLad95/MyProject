// ===============================
// changePassword.js
// ===============================

$(document).ready(function () {

    // 🔹 Password strength check
    $('#NewPassword').on('input', function () {
        const password = $(this).val();
        const strength = getPasswordStrength(password);
        displayPasswordStrength(strength);
    });

    // 🔹 Toggle password visibility
    $('.toggle-password').click(function () {
        const targetId = $(this).data('target');
        const $passwordField = $('#' + targetId);
        const $icon = $(this).find('i');

        if ($passwordField.attr('type') === 'password') {
            $passwordField.attr('type', 'text');
            $icon.removeClass('fa-eye').addClass('fa-eye-slash');
            $(this).addClass('btn-primary').removeClass('btn-outline-secondary');
        } else {
            $passwordField.attr('type', 'password');
            $icon.removeClass('fa-eye-slash').addClass('fa-eye');
            $(this).removeClass('btn-primary').addClass('btn-outline-secondary');
        }
    });

    // 🔹 Change Password button click
    $('#btnChangePassword').click(function (e) {
        e.preventDefault();

        const model = {
            Id: $('#UserId').val() || 0,
            OldPassword: $('#OldPassword').val(),
            NewPassword: $('#NewPassword').val(),
            ConfirmPassword: $('#ConfirmPassword').val()
        };

        if (model.NewPassword !== model.ConfirmPassword) {
            showMessage("New password and Confirm password do not match.", "alert-danger");
            return;
        }

        // 🔹 Basic validation before AJAX
        const strength = getPasswordStrength(model.NewPassword);
        if (strength.score < 3) {
            showMessage("Password is too weak. Please include at least 8 characters, a number, and a special symbol.", "alert-danger");
            return;
        }

        const token = localStorage.getItem("accessToken"); // Get token
        $.ajax({
            url: '/api/auth/ChangePassword',
            type: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(token && { 'Authorization': `Bearer ${token}` })
            },
            //contentType: 'application/json',
            data: JSON.stringify(model),
            success: function (response) {
                showSuccessMessage();
                $('#changePasswordForm')[0].reset();
                $('#passwordStrength').html('');
                // Reset all eye icons to default state
                $('.toggle-password').each(function () {
                    const $icon = $(this).find('i');
                    $icon.removeClass('fa-eye-slash').addClass('fa-eye');
                    $(this).removeClass('btn-primary').addClass('btn-outline-secondary');
                });
            },
            error: function (xhr) {
                const msg = xhr.responseJSON?.message || 'Error changing password.';
                showMessage(msg, "alert-danger");
            }
        });
    });

    // 🔹 Back to form button click
    $('#btnBackToForm').click(function () {
        $('#successMessageDiv').addClass('d-none');
        $('#changePasswordForm').removeClass('d-none');
    });

    // ===============================
    // Helper Functions
    // ===============================

    function getPasswordStrength(password) {
        let score = 0;
        if (password.length >= 8) score++;
        if (/[A-Z]/.test(password)) score++;
        if (/[0-9]/.test(password)) score++;
        if (/[^A-Za-z0-9]/.test(password)) score++;
        return { score: score };
    }

    function displayPasswordStrength(strength) {
        const messages = [
            'Very Weak 🔴',
            'Weak 🟠',
            'Moderate 🟡',
            'Strong 🟢'
        ];
        $('#passwordStrength').html(`<small>${messages[strength.score - 1] || ''}</small>`);
    }

    // Show Success Message Div
    function showSuccessMessage() {
        // Hide the form and show success message
        $('#changePasswordForm').addClass('d-none');
        $('#successMessageDiv').removeClass('d-none');
    }

    // Show Regular Toast Message for errors
    function showMessage(message, type) {
        const bgClass = type.replace('alert-', 'bg-');
        const toastHtml = `
            <div class="toast align-items-center text-white ${bgClass} border-0 mb-2" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">${message}</div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>`;
        const $toast = $(toastHtml);
        $(".toast-container").append($toast);
        const toast = new bootstrap.Toast($toast[0], { autohide: true, delay: 5000 });
        toast.show();
        $toast.on('hidden.bs.toast', function () { $(this).remove(); });
    }

    // Logout function (if still needed for other purposes)
    window.logoutNow = function () {
        window.location.href = '/User/Login';
    };
});
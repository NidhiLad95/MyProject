$(document).ready(function () {

    // 🔹 Validate email and mobile format (no required checks)
    window.validateEmailAndMobile = function () {
        let isValid = true;

        // Clear previous messages and borders
        $("#emailError, #mobileError").text("");
        $("#Email, #Mobileno").removeClass("is-invalid");

        const email = $("#Email").val().trim();
        const mobile = $("#Mobileno").val().trim();

        // Regular expressions
        const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        const mobilePattern = /^[6-9]\d{9}$/;

        // ✅ Email format validation (only if entered)
        if (email !== "" && !emailPattern.test(email)) {
            $("#emailError").text("Please enter a valid email address.");
            $("#Email").addClass("is-invalid");
            isValid = false;
        }

        // ✅ Mobile format validation (only if entered)
        if (mobile !== "" && !mobilePattern.test(mobile)) {
            $("#mobileError").text("Please enter a valid 10-digit mobile number.");
            $("#Mobileno").addClass("is-invalid");
            isValid = false;
        }

        return isValid;
    };

});

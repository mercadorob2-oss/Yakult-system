// Clamps fulfillment qty inputs to [0, data-max] on the RequestFulfillment/CartridgeFulfillment
// Fulfill forms. Server-side re-caps against fresh DB state on submit regardless — this is
// purely a UX guard against typing an out-of-range value.
(function () {
    function clamp(input) {
        var max = parseInt(input.getAttribute('data-max'), 10) || 0;
        var value = parseInt(input.value, 10);
        if (isNaN(value)) value = 0;
        if (value < 0) value = 0;
        if (value > max) value = max;
        input.value = value;
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.fulfill-qty-input').forEach(function (input) {
            input.addEventListener('change', function () { clamp(input); });
            input.addEventListener('blur', function () { clamp(input); });
        });
    });
})();

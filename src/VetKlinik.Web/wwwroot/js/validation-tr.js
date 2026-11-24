// jQuery Validate ondalık sayıda yalnızca nokta kabul eder; Türkçe "499,99" yazımına izin verelim.
if (window.jQuery && $.validator) {
    $.validator.methods.number = function (value, element) {
        return this.optional(element) || /^-?\d+([.,]\d+)?$/.test(value);
    };
    $.validator.methods.range = function (value, element, param) {
        var number = parseFloat(String(value).replace(",", "."));
        return this.optional(element) || (number >= param[0] && number <= param[1]);
    };
}

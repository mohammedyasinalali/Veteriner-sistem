// Ondalık ayırıcı yalnızca virgül ("499,99"); sunucudaki TurkishDecimalModelBinder ile aynı kural.
if (window.jQuery && $.validator) {
    $.validator.methods.number = function (value, element) {
        return this.optional(element) || /^-?\d+(,\d+)?$/.test(value);
    };
    $.validator.methods.range = function (value, element, param) {
        var number = parseFloat(String(value).replace(",", "."));
        return this.optional(element) || (number >= param[0] && number <= param[1]);
    };
}

// Fotoğraf alanı: PhotoFileAttribute'un tarayıcı tarafı (uzantı ve boyut).
if (window.jQuery && $.validator && $.validator.unobtrusive) {
    $.validator.addMethod("photo", function (value, element, param) {
        if (!element.files || element.files.length === 0) return true;
        var file = element.files[0];
        var dot = file.name.lastIndexOf(".");
        var extension = dot >= 0 ? file.name.substring(dot).toLowerCase() : "";
        return file.size <= param.maxbytes && param.extensions.indexOf(extension) >= 0;
    });
    $.validator.unobtrusive.adapters.add("photo", ["maxbytes", "extensions"], function (options) {
        options.rules.photo = {
            maxbytes: parseInt(options.params.maxbytes, 10),
            extensions: options.params.extensions.split(",")
        };
        options.messages.photo = options.message;
    });
}

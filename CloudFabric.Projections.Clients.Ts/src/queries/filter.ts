import {FilterConnector} from "./filterConnector";
import {FilterLogic} from "./filterLogic";

export class Filter {
    public filters: Array<FilterConnector>;
    public propertyName: string;
    public operator: string;
    public value: any;
    public visible: boolean;
    public tag: string;

    public constructor(propertyName: string, operator: string, value: Object, tag: string = "") {
        this.propertyName = propertyName;
        this.operator = operator;
        this.value = value;
        this.tag = tag;
        
        this.filters = new Array<FilterConnector>();
    }

    public or(f: Filter): Filter {
        let connector = new FilterConnector(FilterLogic.or, f);
        this.filters.push(connector);
        return this;
    }

    public and(f: Filter): Filter {
        let connector = new FilterConnector(FilterLogic.and, f);
        this.filters.push(connector);
        return this;
    }

    public static desanitizeValue(value: string): string {
        return decodeURIComponent(value)
            .replace(";dot;", ".")
            .replace(";amp;", "&")
            .replace(";excl;", "!")
            .replace(";dollar;", "$")
            .replace(";aps;", "'");
    }

    public static sanitizeValue(value: string): string {
        return value
            .replace(".", ";dot;")
            .replace("&", ";amp;")
            .replace("!", ";excl;")
            .replace("$", ";dollar;")
            .replace("'", ";aps;");
    }

    public serialize(): string {
        let valueSerialized = "";
        if ((this.value != null)) {
            valueSerialized = this.value.toString();
            
            valueSerialized = Filter.sanitizeValue(valueSerialized);
            if ((this.value instanceof String)) {
                valueSerialized = `'${valueSerialized}'`;
            }
        }

        let filtersSerialized = "";
        if (((this.filters != null)
            && (this.filters.length > 0))) {
            filtersSerialized = this.filters.map(f => f.serialize()).join('.');
        }

        return `${this.propertyName ? Filter.sanitizeValue(this.propertyName) : "*"}` +
            `|${this.operator ? this.operator : "*"}` +
            `|${encodeURIComponent(valueSerialized)}` +
            `|${this.visible ? 'T' : 'F'}` +
            `|${encodeURIComponent(this.tag)}` +
            `|${filtersSerialized}`;
    }

    public static deserialize(f: string): Filter {
        let propertyNameEnd = f.indexOf("|");
        let propertyName = Filter.desanitizeValue(f.substring(0, propertyNameEnd));
        let operatorEnd = f.indexOf("|", propertyNameEnd + 1);
        let operatorValue = f.substring(propertyNameEnd + 1, operatorEnd);
        let valueEnd = f.indexOf("|", operatorEnd + 1);
        let value = f.substring(operatorEnd + 1, valueEnd);
        let visibleEnd = f.indexOf("|", (valueEnd + 1));
        let visible = f.substring(valueEnd + 1, visibleEnd) == 'T';
        let tagEnd = f.indexOf("|", visibleEnd + 1);
        let tag = f.substring(visibleEnd + 1, tagEnd);
        tag = decodeURIComponent(tag);

        value = Filter.desanitizeValue(value);

        let filters = new Array<FilterConnector>();
        let filtersSerializedList = f.substring(tagEnd + 1).split('.');
        if ((filtersSerializedList.length > 0)) {
            filters = filtersSerializedList.filter(f => f.length > 0).map(f => FilterConnector.Deserialize(f));
        }

        let filter = new Filter(propertyName, operatorValue, null, tag);
        filter.visible = visible;
        filter.filters = filters;

        if ((value.indexOf("'") == 0)) {
            filter.value = value.replace("'", "");
        } else if (value.indexOf('.') > -1) {
            filter.value = parseFloat(value);
        } else {
            filter.value = parseInt(value);
        } // TODO: parse DateTime

        return filter;
    }
}
    
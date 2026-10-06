// students - HTTP trigger + Azure SQL input binding.
// The SQL query and connection are defined in function.json; rows arrive here as "students".
module.exports = async function (context, req, students) {
    context.res = {
        headers: { 'Content-Type': 'application/json' },
        body: students
    };
};
